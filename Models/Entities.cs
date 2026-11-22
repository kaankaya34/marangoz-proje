using System.ComponentModel.DataAnnotations;

namespace web.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FullName { get; set; } = "";

    [Required, StringLength(150)]
    public string Email { get; set; } = "";

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(300)]
    public string PasswordHash { get; set; } = "";

    [Required, StringLength(20)]
    public string Role { get; set; } = Roles.Customer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // KVKK aydınlatma metninin onaylandığı an (panelden eklenen hesaplarda boştur).
    public DateTime? KvkkAcceptedAt { get; set; }

    public List<QuoteRequest> QuoteRequests { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori adı zorunludur."), StringLength(100)]
    [Display(Name = "Kategori Adı")]
    public string Name { get; set; } = "";

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    public List<Product> Products { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur."), StringLength(150)]
    [Display(Name = "Ürün Adı")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Açıklama zorunludur."), StringLength(4000)]
    [Display(Name = "Açıklama")]
    public string Description { get; set; } = "";

    [StringLength(100)]
    [Display(Name = "Ahşap Türü")]
    public string? Material { get; set; }

    [StringLength(100)]
    [Display(Name = "Ölçüler")]
    public string? Dimensions { get; set; }

    [Range(0, 10000000, ErrorMessage = "Geçerli bir fiyat girin.")]
    [Display(Name = "Başlangıç Fiyatı (₺)")]
    public decimal? Price { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Kategori")]
    [Range(1, int.MaxValue, ErrorMessage = "Kategori seçin.")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Display(Name = "Yayında")]
    public bool IsActive { get; set; } = true;

    public int? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ProductImage> Images { get; set; } = new();
}

public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, StringLength(300)]
    public string Url { get; set; } = "";

    public int SortOrder { get; set; }
}

public class QuoteRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur."), StringLength(150)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Talep açıklaması zorunludur."), StringLength(4000, MinimumLength = 10, ErrorMessage = "Açıklama en az 10 karakter olmalıdır.")]
    [Display(Name = "Talep Açıklaması")]
    public string Description { get; set; } = "";

    [StringLength(200)]
    [Display(Name = "İstenen Ölçüler")]
    public string? CustomDimensions { get; set; }

    [Range(0, 10000000)]
    [Display(Name = "Tahmini Bütçe (₺)")]
    public decimal? Budget { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Pending;

    // Atölyenin verdiği teklif
    [Range(0, 100000000)]
    public decimal? QuotedPrice { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    [StringLength(1000)]
    public string? WorkshopNote { get; set; }

    // Müşterinin teklife yanıtı
    public CustomerResponse CustomerResponse { get; set; } = CustomerResponse.None;
    [StringLength(1000)]
    public string? CustomerResponseNote { get; set; }
    public DateTime? CustomerRespondedAt { get; set; }

    public List<QuoteAttachment> Attachments { get; set; } = new();
    public List<QuoteStatusHistory> History { get; set; } = new();

    // Yapay zeka analizi çıktıları
    [StringLength(50)]
    public string? AiCategory { get; set; }
    [StringLength(20)]
    public string? AiPriority { get; set; }
    public int? AiEstimatedDays { get; set; }
    [StringLength(1000)]
    public string? AiSummary { get; set; }
    [StringLength(300)]
    public string? AiKeywords { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

// Müşterinin yüklediği referans fotoğraf / ölçü krokisi
public class QuoteAttachment
{
    public int Id { get; set; }

    public int QuoteRequestId { get; set; }
    public QuoteRequest? QuoteRequest { get; set; }

    [Required, StringLength(300)]
    public string Url { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Durum geçişleri ve atölye notları / üretim aşaması fotoğrafları
public class QuoteStatusHistory
{
    public int Id { get; set; }

    public int QuoteRequestId { get; set; }
    public QuoteRequest? QuoteRequest { get; set; }

    public QuoteStatus Status { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [StringLength(100)]
    public string ChangedBy { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Panelden düzenlenen tek satırlık site ayarı (Id = 1)
public class SiteSetting
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Atölye adı zorunludur."), StringLength(100)]
    [Display(Name = "Atölye Adı")]
    public string Name { get; set; } = "";

    [StringLength(30)]
    [Display(Name = "Sipariş Telefonu")]
    public string? OrderPhone { get; set; }

    [EmailAddress, StringLength(150)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [StringLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [StringLength(30)]
    [Display(Name = "WhatsApp Numarası")]
    public string? WhatsApp { get; set; }

    [StringLength(200)]
    [Display(Name = "Instagram Bağlantısı")]
    public string? Instagram { get; set; }

    [StringLength(500)]
    [Display(Name = "Harita Bağlantısı")]
    public string? MapUrl { get; set; }

    [StringLength(200)]
    [Display(Name = "Çalışma Saatleri")]
    public string? WorkingHours { get; set; }
}

public class ActivityLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }

    [StringLength(100)]
    public string UserName { get; set; } = "";

    [StringLength(20)]
    public string UserRole { get; set; } = "";

    [StringLength(50)]
    public string Action { get; set; } = "";

    [StringLength(50)]
    public string EntityType { get; set; } = "";

    public int? EntityId { get; set; }

    [StringLength(1000)]
    public string Details { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
