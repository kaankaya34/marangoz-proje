namespace web.Models;

public enum QuoteStatus
{
    Pending = 0,
    Surveyed = 1,
    InProduction = 2,
    Finishing = 3,
    Completed = 4,
    Cancelled = 5
}

public enum CustomerResponse
{
    None = 0,
    Approved = 1,
    RevisionRequested = 2,
    Rejected = 3
}

public static class QuoteStatusInfo
{
    public static string Label(this QuoteStatus s) => s switch
    {
        QuoteStatus.Pending => "Beklemede / İnceleniyor",
        QuoteStatus.Surveyed => "Keşif / Ölçü Alındı",
        QuoteStatus.InProduction => "Atölyede Üretimde",
        QuoteStatus.Finishing => "Cila / Boya Aşamasında",
        QuoteStatus.Completed => "Tamamlandı / Teslim Edildi",
        QuoteStatus.Cancelled => "İptal Edildi",
        _ => s.ToString()
    };

    public static string Icon(this QuoteStatus s) => s switch
    {
        QuoteStatus.Pending => "🟡",
        QuoteStatus.Surveyed => "🔵",
        QuoteStatus.InProduction => "🟠",
        QuoteStatus.Finishing => "🟤",
        QuoteStatus.Completed => "🟢",
        _ => "⚫"
    };

    // Renk sınıfları wwwroot/css/site.css içinde tanımlıdır.
    public static string CssClass(this QuoteStatus s) => s switch
    {
        QuoteStatus.Pending => "status-pending",
        QuoteStatus.Surveyed => "status-surveyed",
        QuoteStatus.InProduction => "status-production",
        QuoteStatus.Finishing => "status-finishing",
        QuoteStatus.Completed => "status-completed",
        _ => "status-cancelled"
    };

    public static string Label(this CustomerResponse r) => r switch
    {
        CustomerResponse.Approved => "Müşteri onayladı",
        CustomerResponse.RevisionRequested => "Müşteri revize istedi",
        CustomerResponse.Rejected => "Müşteri reddetti",
        _ => "Müşteri yanıtı bekleniyor"
    };
}
