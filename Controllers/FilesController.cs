using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Services;

namespace web.Controllers;

// Müşteri fotoğrafları ve atölye notu görselleri herkese açık klasörde değil, yalnızca talep sahibine ve personele sunulur.
[Authorize]
public class FilesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IImageStorage _images;

    public FilesController(AppDbContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
    }

    [HttpGet("files/{name}")]
    public async Task<IActionResult> Get(string name)
    {
        var path = _images.GetPrivatePath(name);
        if (path is null) return NotFound();

        if (!User.IsStaff())
        {
            var userId = User.GetUserId();
            var url = ImageStorage.PrivateUrlPrefix + name;
            var owned = await _db.QuoteAttachments.AnyAsync(a => a.Url == url && a.QuoteRequest!.UserId == userId)
                     || await _db.QuoteStatusHistories.AnyAsync(h => h.ImageUrl == url && h.QuoteRequest!.UserId == userId);
            if (!owned) return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=3600";
        var contentType = Path.GetExtension(name) switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return PhysicalFile(path, contentType);
    }
}
