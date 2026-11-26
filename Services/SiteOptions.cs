using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using web.Data;
using web.Models;

namespace web.Services;

// appsettings.json "Site" bölümü varsayılan değerleri verir; panelden kaydedilen ayarlar bunların üzerine yazılır.
public class SiteOptions
{
    public string Name { get; set; } = "Ahşap Atölyesi";
    public string OrderPhone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string MapUrl { get; set; } = "";
    public string WorkingHours { get; set; } = "";

    // wa.me için ülke kodlu rakamlar (905XXXXXXXXX); geçersizse null.
    public string? WhatsAppDigits
        => PhoneHelper.TryNormalize(WhatsApp, out var n) ? "90" + n[1..] : null;
}

public interface ISiteSettings
{
    SiteOptions Value { get; }
    void Invalidate();
}

public class SiteSettingsProvider : ISiteSettings
{
    private const string CacheKey = "site-settings";
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<SiteOptions> _defaults;
    private readonly IMemoryCache _cache;

    public SiteSettingsProvider(IServiceScopeFactory scopes, IOptions<SiteOptions> defaults, IMemoryCache cache)
    {
        _scopes = scopes;
        _defaults = defaults;
        _cache = cache;
    }

    public SiteOptions Value => _cache.GetOrCreate(CacheKey, entry =>
    {
        entry.Size = 1;
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        try
        {
            using var scope = _scopes.CreateScope();
            var row = scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteSettings.AsNoTracking().FirstOrDefault();
            if (row is not null)
            {
                return new SiteOptions
                {
                    Name = row.Name,
                    OrderPhone = row.OrderPhone ?? "",
                    Email = row.Email ?? "",
                    Address = row.Address ?? "",
                    WhatsApp = row.WhatsApp ?? "",
                    Instagram = row.Instagram ?? "",
                    MapUrl = row.MapUrl ?? "",
                    WorkingHours = row.WorkingHours ?? ""
                };
            }
        }
        catch
        {
            // Tablo henüz yoksa (migration çalışmadan önce) varsayılanlarla devam edilir.
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
        }
        return _defaults.Value;
    })!;

    public void Invalidate() => _cache.Remove(CacheKey);
}
