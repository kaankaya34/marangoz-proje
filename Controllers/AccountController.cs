using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using web.Data;
using web.Models;
using web.Services;

namespace web.Controllers;

public class AccountController : Controller
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IActivityLogger _log;
    private readonly IMemoryCache _cache;

    public AccountController(AppDbContext db, IPasswordService passwords, IActivityLogger log, IMemoryCache cache)
    {
        _db = db;
        _passwords = passwords;
        _log = log;
        _cache = cache;
    }

    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Profile") : View(new RegisterViewModel());

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!model.AcceptKvkk)
            ModelState.AddModelError(nameof(model.AcceptKvkk), "Kayıt için KVKK Aydınlatma Metni'ni onaylamalısınız.");
        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta adresi zaten kayıtlı.");
            return View(model);
        }

        // Rol istemciden alınmaz; kayıt olan herkes Müşteri olur.
        var user = new AppUser
        {
            FullName = model.FullName.Trim(),
            Email = email,
            Phone = PhoneHelper.TryNormalize(model.Phone, out var phone) ? phone : null,
            PasswordHash = _passwords.Hash(model.Password),
            Role = Roles.Customer,
            KvkkAcceptedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInAsync(user, isPersistent: false);
        await _log.LogAsync(LogActions.Register, "Kullanıcı", user.Id, $"Yeni müşteri kaydı: {user.Email}");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
        => User.Identity?.IsAuthenticated == true ? RedirectToAction("Profile") : View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var failKey = $"login-fail:{HttpContext.Connection.RemoteIpAddress}|{email}";
        if (_cache.TryGetValue(failKey, out int fails) && fails >= MaxFailedLogins)
        {
            ModelState.AddModelError("", "Çok fazla başarısız deneme. Lütfen 15 dakika sonra tekrar deneyin.");
            return View(model);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Kullanıcı yoksa da hash doğrulaması yapılır; böylece yanıt süresiyle hesap varlığı sızdırılmaz.
        var ok = _passwords.Verify(model.Password, user?.PasswordHash ?? _passwords.DummyHash) && user is not null;

        if (!ok)
        {
            _cache.Set(failKey, fails + 1, LockoutWindow);
            ModelState.AddModelError("", "E-posta veya şifre hatalı.");
            return View(model);
        }

        _cache.Remove(failKey);
        await SignInAsync(user!, model.RememberMe);
        await _log.LogAsync(LogActions.Login, "Kullanıcı", user!.Id, $"{user.Email} giriş yaptı.");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return user.Role is Roles.Manager or Roles.Admin
            ? RedirectToAction("Index", "Dashboard", new { area = "Panel" })
            : RedirectToAction("Profile");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var id = User.GetUserId()!.Value;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return Challenge();

        var vm = new ProfileViewModel
        {
            User = user,
            Quotes = await _db.QuoteRequests
                .AsNoTracking()
                .Include(q => q.Product)
                .Where(q => q.UserId == id)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync()
        };
        return View(vm);
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> EditProfile()
    {
        var user = await CurrentUserAsync();
        return user is null ? Challenge() : View(new ProfileEditViewModel { FullName = user.FullName, Phone = user.Phone });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> EditProfile(ProfileEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();

        user.FullName = model.FullName.Trim();
        user.Phone = PhoneHelper.TryNormalize(model.Phone, out var phone) ? phone : null;
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.ProfileUpdated, "Kullanıcı", user.Id, $"{user.Email} profilini güncelledi.");

        // Ad değiştiği için çerezdeki kimlik bilgisi yenilenir; kalıcı oturum tercihi korunur.
        var auth = await HttpContext.AuthenticateAsync("Cookies");
        await SignInAsync(user, auth.Properties?.IsPersistent ?? false);

        TempData["Success"] = "Profiliniz güncellendi.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet, Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, Authorize, EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();

        if (!_passwords.Verify(model.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut şifre hatalı.");
            return View(model);
        }

        user.PasswordHash = _passwords.Hash(model.NewPassword);
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.PasswordChanged, "Kullanıcı", user.Id, $"{user.Email} şifresini değiştirdi.");

        TempData["Success"] = "Şifreniz değiştirildi.";
        return RedirectToAction(nameof(Profile));
    }

    private Task<AppUser?> CurrentUserAsync()
    {
        var id = User.GetUserId();
        return _db.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    private async Task SignInAsync(AppUser user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties { IsPersistent = isPersistent });
        HttpContext.User = principal; // Aynı istekte yazılacak log kayıtlarında kullanıcı adı görünsün.
    }
}
