using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services;

namespace web.Areas.Panel.Controllers;

[Authorize(Policy = "AdminOnly")]
public class SettingsController : PanelControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISiteSettings _site;
    private readonly IActivityLogger _log;

    public SettingsController(AppDbContext db, ISiteSettings site, IActivityLogger log)
    {
        _db = db;
        _site = site;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var row = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
        if (row is not null) return View(row);

        var d = _site.Value;
        return View(new SiteSetting
        {
            Name = d.Name, OrderPhone = d.OrderPhone, Email = d.Email, Address = d.Address,
            WhatsApp = d.WhatsApp, Instagram = d.Instagram, MapUrl = d.MapUrl, WorkingHours = d.WorkingHours
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(SiteSetting model)
    {
        // Bağlantılar sayfada href olarak kullanıldığı için yalnızca http/https kabul edilir.
        ValidateUrl(nameof(model.Instagram), model.Instagram);
        ValidateUrl(nameof(model.MapUrl), model.MapUrl);
        if (!string.IsNullOrWhiteSpace(model.WhatsApp) && !PhoneHelper.TryNormalize(model.WhatsApp, out _))
            ModelState.AddModelError(nameof(model.WhatsApp), "Geçerli bir cep telefonu numarası girin (05XX XXX XX XX).");

        if (!ModelState.IsValid) return View(model);

        var row = await _db.SiteSettings.FirstOrDefaultAsync();
        if (row is null)
        {
            row = new SiteSetting();
            _db.SiteSettings.Add(row);
        }

        row.Name = model.Name.Trim();
        row.OrderPhone = (model.OrderPhone ?? "").Trim();
        row.Email = (model.Email ?? "").Trim();
        row.Address = (model.Address ?? "").Trim();
        row.WhatsApp = (model.WhatsApp ?? "").Trim();
        row.Instagram = (model.Instagram ?? "").Trim();
        row.MapUrl = (model.MapUrl ?? "").Trim();
        row.WorkingHours = (model.WorkingHours ?? "").Trim();
        await _db.SaveChangesAsync();
        _site.Invalidate();
        await _log.LogAsync(LogActions.SettingsUpdated, "Ayarlar", row.Id, "Site ayarları güncellendi.");

        TempData["Success"] = "Site ayarları kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateUrl(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            ModelState.AddModelError(field, "http:// veya https:// ile başlayan geçerli bir bağlantı girin.");
    }
}
