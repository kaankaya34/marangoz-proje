using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services;

namespace web.Areas.Panel.Controllers;

public class UsersController : PanelControllerBase
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IActivityLogger _log;
    private readonly IUserSessionCache _sessions;
    private readonly IImageStorage _images;

    public UsersController(AppDbContext db, IPasswordService passwords, IActivityLogger log, IUserSessionCache sessions, IImageStorage images)
    {
        _db = db;
        _passwords = passwords;
        _log = log;
        _sessions = sessions;
        _images = images;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new UserListViewModel
        {
            Items = await query.OrderBy(u => u.Role).ThenBy(u => u.FullName).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Query = q,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new UserCreateViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        // Yönetici yalnızca Müşteri ekleyebilir; Yönetici/Admin hesabı oluşturmak Admin yetkisindedir.
        if (!User.IsInRole(Roles.Admin)) model.Role = Roles.Customer;
        if (!Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Geçersiz rol.");

        var email = model.Email.Trim().ToLowerInvariant();
        if (ModelState.IsValid && await _db.Users.AnyAsync(u => u.Email == email))
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta adresi zaten kayıtlı.");

        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            FullName = model.FullName.Trim(),
            Email = email,
            Phone = PhoneHelper.TryNormalize(model.Phone, out var phone) ? phone : null,
            PasswordHash = _passwords.Hash(model.Password),
            Role = model.Role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.UserCreated, "Kullanıcı", user.Id, $"Kullanıcı eklendi: {user.Email} ({Roles.DisplayName(user.Role)})");

        TempData["Success"] = "Kullanıcı eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (!CanManage(user)) return Denied();

        return View(new UserEditViewModel { Id = user.Id, Email = user.Email, FullName = user.FullName, Phone = user.Phone, Role = user.Role });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserEditViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (!CanManage(user)) return Denied();

        model.Id = id;
        model.Email = user.Email;

        // Rol yalnızca Admin tarafından ve kendi hesabı dışında değiştirilebilir.
        var roleChanged = false;
        if (User.IsInRole(Roles.Admin) && id != User.GetUserId() && model.Role != user.Role)
        {
            if (!Roles.All.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Geçersiz rol.");
            else
                roleChanged = true;
        }
        else
        {
            model.Role = user.Role;
        }

        if (!ModelState.IsValid) return View(model);

        var oldRole = user.Role;
        user.FullName = model.FullName.Trim();
        user.Phone = PhoneHelper.TryNormalize(model.Phone, out var phone) ? phone : null;
        if (roleChanged) user.Role = model.Role;
        await _db.SaveChangesAsync();
        _sessions.Invalidate(user.Id);

        var detail = $"Kullanıcı güncellendi: {user.Email}" + (roleChanged ? $" (rol: {Roles.DisplayName(oldRole)} → {Roles.DisplayName(user.Role)})" : "");
        await _log.LogAsync(LogActions.UserUpdated, "Kullanıcı", user.Id, detail);

        TempData["Success"] = "Kullanıcı güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (!CanManage(user)) return Denied();
        return View(new PasswordResetViewModel { Id = user.Id, FullName = user.FullName });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, PasswordResetViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (!CanManage(user)) return Denied();

        model.Id = id;
        model.FullName = user.FullName;
        if (!ModelState.IsValid) return View(model);

        user.PasswordHash = _passwords.Hash(model.NewPassword);
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.PasswordReset, "Kullanıcı", user.Id, $"{user.Email} kullanıcısının şifresi sıfırlandı.");

        TempData["Success"] = "Şifre güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (id == User.GetUserId())
        {
            TempData["Error"] = "Kendi hesabınızı silemezsiniz.";
            return RedirectToAction(nameof(Index));
        }

        // Yönetici yalnızca Müşteri silebilir; Yönetici/Admin hesaplarını yalnızca Admin silebilir.
        if (!CanManage(user))
        {
            TempData["Error"] = "Bu kullanıcıyı silme yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }

        // Talepler cascade silineceği için diskteki dosyaları da temizlenir.
        var files = await _db.QuoteAttachments.Where(a => a.QuoteRequest!.UserId == id).Select(a => a.Url)
            .Concat(_db.QuoteStatusHistories.Where(h => h.QuoteRequest!.UserId == id && h.ImageUrl != null).Select(h => h.ImageUrl!))
            .ToListAsync();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        _sessions.Invalidate(id);
        foreach (var f in files) _images.DeleteIfLocal(f);
        await _log.LogAsync(LogActions.UserDeleted, "Kullanıcı", id, $"Kullanıcı silindi: {user.Email} ({Roles.DisplayName(user.Role)}). Talepleri de silindi.");

        TempData["Success"] = "Kullanıcı silindi.";
        return RedirectToAction(nameof(Index));
    }

    // Admin herkesi yönetir; Yönetici yalnızca Müşteri hesaplarını.
    private bool CanManage(AppUser target) => User.IsInRole(Roles.Admin) || target.Role == Roles.Customer;

    private IActionResult Denied()
    {
        TempData["Error"] = "Bu kullanıcı üzerinde işlem yapma yetkiniz yok.";
        return RedirectToAction(nameof(Index));
    }
}
