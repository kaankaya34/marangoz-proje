using System.Security.Claims;
using web.Data;
using web.Models;

namespace web.Services;

public static class UserExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsStaff(this ClaimsPrincipal user)
        => user.IsInRole(Roles.Manager) || user.IsInRole(Roles.Admin);
}

public static class DateExtensions
{
    // Veritabanında UTC saklanır; Türkiye'de yaz saati uygulaması olmadığı için sabit UTC+3 kullanılır.
    public static DateTime ToLocalTr(this DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(3);
    public static string ToTrString(this DateTime utc) => utc.ToLocalTr().ToString("dd.MM.yyyy HH:mm");
}

public interface IActivityLogger
{
    Task LogAsync(string action, string entityType, int? entityId, string details);
}

public class ActivityLogger : IActivityLogger
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public ActivityLogger(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityType, int? entityId, string details)
    {
        var user = _http.HttpContext?.User;
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = user?.GetUserId(),
            UserName = Truncate(user?.Identity?.Name ?? "Sistem", 100),
            UserRole = user?.FindFirstValue(ClaimTypes.Role) ?? "",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = Truncate(details, 1000),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}

public static class LogActions
{
    public const string Login = "Giriş";
    public const string Register = "Kayıt";
    public const string ProductCreated = "Ürün Eklendi";
    public const string ProductUpdated = "Ürün Güncellendi";
    public const string ProductToggled = "Ürün Durumu Değişti";
    public const string CategoryCreated = "Kategori Eklendi";
    public const string CategoryDeleted = "Kategori Silindi";
    public const string QuoteCreated = "Talep Oluşturuldu";
    public const string QuoteStatusUpdated = "Teklif Durumu Güncellendi";
    public const string QuoteCancelled = "Talep İptal Edildi";
    public const string UserCreated = "Kullanıcı Eklendi";
    public const string UserDeleted = "Kullanıcı Silindi";
    public const string ProductDeleted = "Ürün Silindi";
    public const string CategoryUpdated = "Kategori Güncellendi";
    public const string UserUpdated = "Kullanıcı Güncellendi";
    public const string PasswordReset = "Şifre Sıfırlandı";
    public const string PasswordChanged = "Şifre Değiştirildi";
    public const string ProfileUpdated = "Profil Güncellendi";
    public const string QuotePriced = "Teklif Fiyatı Verildi";
    public const string QuoteNoted = "Teklif Notu Eklendi";
    public const string QuoteCustomerResponse = "Müşteri Teklif Yanıtı";
    public const string SettingsUpdated = "Site Ayarları Güncellendi";
}
