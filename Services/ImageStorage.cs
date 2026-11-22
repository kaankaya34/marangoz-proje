namespace web.Services;

public interface IImageStorage
{
    // Başarılıysa (url, null), hatalıysa (null, hata mesajı) döner.
    Task<(string? Url, string? Error)> SaveImageAsync(IFormFile file);
    void DeleteIfLocal(string? url);
}

public class ImageStorage : IImageStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const string UploadFolder = "uploads";
    private readonly IWebHostEnvironment _env;

    public ImageStorage(IWebHostEnvironment env) => _env = env;

    public async Task<(string? Url, string? Error)> SaveImageAsync(IFormFile file)
    {
        if (file.Length == 0) return (null, "Dosya boş.");
        if (file.Length > MaxBytes) return (null, "Görsel en fazla 5 MB olabilir.");

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header);
        var ext = DetectExtension(header.AsSpan(0, read));
        if (ext is null) return (null, "Yalnızca jpg, png veya webp görseller yüklenebilir.");

        var dir = Path.Combine(_env.WebRootPath, UploadFolder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";

        stream.Position = 0;
        await using var target = File.Create(Path.Combine(dir, name));
        await stream.CopyToAsync(target);
        return ($"/{UploadFolder}/{name}", null);
    }

    public void DeleteIfLocal(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith($"/{UploadFolder}/")) return;
        var path = Path.Combine(_env.WebRootPath, UploadFolder, Path.GetFileName(url));
        if (File.Exists(path)) File.Delete(path);
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
