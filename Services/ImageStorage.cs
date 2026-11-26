namespace web.Services;

public interface IImageStorage
{
    // Başarılıysa (url, null), hatalıysa (null, hata mesajı) döner.
    // isPrivate: dosya wwwroot dışına (App_Data/private-uploads) yazılır ve yalnızca yetkili kullanıcılara /files/{ad} ile sunulur.
    Task<(string? Url, string? Error)> SaveImageAsync(IFormFile file, bool isPrivate = false);
    void DeleteIfLocal(string? url);
    // Özel dosyanın disk yolunu döner; ad geçersizse veya dosya yoksa null.
    string? GetPrivatePath(string name);
    // Herkese açık /uploads/ dosyasını özel klasöre taşır, yeni /files/ adresini döner; dosya yoksa null.
    string? MoveToPrivate(string publicUrl);
    // wwwroot/uploads altındaki dosyaların herkese açık adresleri.
    IReadOnlyList<string> ListPublicUploads();
}

public class ImageStorage : IImageStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const string UploadFolder = "uploads";
    public const string PrivateUrlPrefix = "/files/";
    private static readonly System.Text.RegularExpressions.Regex SafeName = new(@"^[0-9a-f]{32}\.(jpg|png|webp)$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private readonly IWebHostEnvironment _env;

    public ImageStorage(IWebHostEnvironment env) => _env = env;

    public async Task<(string? Url, string? Error)> SaveImageAsync(IFormFile file, bool isPrivate = false)
    {
        if (file.Length == 0) return (null, "Dosya boş.");
        if (file.Length > MaxBytes) return (null, "Görsel en fazla 5 MB olabilir.");

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header);
        var ext = DetectExtension(header.AsSpan(0, read));
        if (ext is null) return (null, "Yalnızca jpg, png veya webp görseller yüklenebilir.");

        stream.Position = 0;
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer);
        var clean = ImageMetadataStripper.Strip(buffer.ToArray(), ext);
        if (clean is null) return (null, "Görsel dosyası okunamadı veya bozuk.");

        var dir = isPrivate ? PrivateDirectory : Path.Combine(_env.WebRootPath, UploadFolder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        await File.WriteAllBytesAsync(Path.Combine(dir, name), clean);
        return (isPrivate ? $"{PrivateUrlPrefix}{name}" : $"/{UploadFolder}/{name}", null);
    }

    public string PrivateDirectory => Path.Combine(_env.ContentRootPath, "App_Data", "private-uploads");

    public IReadOnlyList<string> ListPublicUploads()
    {
        var dir = Path.Combine(_env.WebRootPath, UploadFolder);
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.EnumerateFiles(dir).Select(f => $"/{UploadFolder}/{Path.GetFileName(f)}").ToList();
    }

    public string? MoveToPrivate(string publicUrl)
    {
        if (!publicUrl.StartsWith($"/{UploadFolder}/")) return null;
        var name = Path.GetFileName(publicUrl);
        if (!SafeName.IsMatch(name)) return null;
        var source = Path.Combine(_env.WebRootPath, UploadFolder, name);
        if (!File.Exists(source)) return null;

        Directory.CreateDirectory(PrivateDirectory);
        var target = Path.Combine(PrivateDirectory, name);
        if (!File.Exists(target)) File.Move(source, target); else File.Delete(source);
        return PrivateUrlPrefix + name;
    }

    public string? GetPrivatePath(string name)
    {
        if (!SafeName.IsMatch(name)) return null;
        var path = Path.Combine(PrivateDirectory, name);
        return File.Exists(path) ? path : null;
    }

    public void DeleteIfLocal(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        string? path = null;
        if (url.StartsWith($"/{UploadFolder}/")) path = Path.Combine(_env.WebRootPath, UploadFolder, Path.GetFileName(url));
        else if (url.StartsWith(PrivateUrlPrefix)) path = GetPrivatePath(Path.GetFileName(url));
        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    // Uzantıya değil dosya imzasına (magic bytes) güvenilir.
    private static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
}
