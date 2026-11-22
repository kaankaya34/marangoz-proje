using System.ComponentModel.DataAnnotations;

namespace web.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Ad soyad zorunludur."), StringLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(150)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [TurkishPhone, StringLength(30)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Şifre zorunludur."), StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre (Tekrar)")]
    public string ConfirmPassword { get; set; } = "";

    [Display(Name = "KVKK Aydınlatma Metni'ni okudum, kabul ediyorum")]
    public bool AcceptKvkk { get; set; }
}

public class ProfileEditViewModel
{
    [Required(ErrorMessage = "Ad soyad zorunludur."), StringLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [TurkishPhone, StringLength(30)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut Şifre")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Yeni şifre zorunludur."), StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Şifre")]
    public string NewPassword { get; set; } = "";

    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Şifre (Tekrar)")]
    public string ConfirmPassword { get; set; } = "";
}

public class LoginViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    public AppUser User { get; set; } = null!;
    public List<QuoteRequest> Quotes { get; set; } = new();
}

public class QuoteCreateViewModel
{
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur."), StringLength(150)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Talep açıklaması zorunludur."), StringLength(4000, MinimumLength = 10, ErrorMessage = "Açıklama en az 10 karakter olmalıdır.")]
    [Display(Name = "Talep Açıklaması")]
    public string Description { get; set; } = "";

    [StringLength(200)]
    [Display(Name = "İstenen Ölçüler")]
    public string? CustomDimensions { get; set; }

    [Range(0, 10000000, ErrorMessage = "Geçerli bir bütçe girin.")]
    [Display(Name = "Tahmini Bütçe (₺)")]
    public decimal? Budget { get; set; }

    [Display(Name = "Referans fotoğraf / ölçü krokisi (en fazla 5 adet)")]
    public List<IFormFile>? Attachments { get; set; }
}

public class ProductListViewModel
{
    public List<Product> Items { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? Query { get; set; }
    public int? CategoryId { get; set; }
    public string Sort { get; set; } = "new";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class HomeViewModel
{
    public List<Category> Categories { get; set; } = new();
    public List<Product> LatestProducts { get; set; } = new();
}

public class ProductFormViewModel
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

    [Display(Name = "Kategori")]
    [Range(1, int.MaxValue, ErrorMessage = "Kategori seçin.")]
    public int CategoryId { get; set; }

    [Display(Name = "Yayında")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Ana görsel (jpg, png, webp - en fazla 5 MB)")]
    public IFormFile? Image { get; set; }

    [Display(Name = "Galeri fotoğrafları (birden fazla seçilebilir)")]
    public List<IFormFile>? GalleryImages { get; set; }

    public bool RemoveMainImage { get; set; }
    public List<int> RemoveImageIds { get; set; } = new();

    public string? ExistingImageUrl { get; set; }
    public List<ProductImage> ExistingGallery { get; set; } = new();

    public List<Category> Categories { get; set; } = new();
}

public class UserEditViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ad soyad zorunludur."), StringLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [TurkishPhone, StringLength(30)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Required]
    [Display(Name = "Rol")]
    public string Role { get; set; } = Roles.Customer;
}

public class PasswordResetViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Yeni şifre zorunludur."), StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Şifre")]
    public string NewPassword { get; set; } = "";
}

public class QuoteOfferViewModel
{
    public int Id { get; set; }

    [Range(0, 100000000, ErrorMessage = "Geçerli bir fiyat girin.")]
    [Display(Name = "Net Fiyat (₺)")]
    public decimal? QuotedPrice { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Tahmini Teslim Tarihi")]
    public DateTime? EstimatedDeliveryDate { get; set; }

    [StringLength(1000)]
    [Display(Name = "Atölye Notu / Mesaj")]
    public string? WorkshopNote { get; set; }
}

public class QuoteProgressViewModel
{
    public int Id { get; set; }

    [Display(Name = "Aşama")]
    public QuoteStatus Status { get; set; }

    [StringLength(500)]
    [Display(Name = "Not")]
    public string? Note { get; set; }

    [Display(Name = "Atölye fotoğrafı")]
    public IFormFile? Image { get; set; }
}

public class PanelProductListViewModel
{
    public List<Product> Items { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? Query { get; set; }
    public int? CategoryId { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class UserListViewModel
{
    public List<AppUser> Items { get; set; } = new();
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "Ad soyad zorunludur."), StringLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(150)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [TurkishPhone, StringLength(30)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Şifre zorunludur."), StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Required]
    [Display(Name = "Rol")]
    public string Role { get; set; } = Roles.Customer;
}

public class DashboardViewModel
{
    public int UserCount { get; set; }
    public int ProductCount { get; set; }
    public int CategoryCount { get; set; }
    public Dictionary<QuoteStatus, int> QuoteCounts { get; set; } = new();
    public List<QuoteRequest> RecentQuotes { get; set; } = new();
}

public class QuoteListViewModel
{
    public List<QuoteRequest> Items { get; set; } = new();
    public QuoteStatus? Status { get; set; }
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}

public class LogListViewModel
{
    public List<ActivityLog> Items { get; set; } = new();
    public string? Action { get; set; }
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public List<string> Actions { get; set; } = new();
}
