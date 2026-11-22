using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services;

namespace web.Areas.Panel.Controllers;

public class ProductsController : PanelControllerBase
{
    private const int PageSize = 15;
    private const int MaxGalleryImages = 10;
    private readonly AppDbContext _db;
    private readonly IActivityLogger _log;
    private readonly IImageStorage _images;

    public ProductsController(AppDbContext db, IActivityLogger log, IImageStorage images)
    {
        _db = db;
        _log = log;
        _images = images;
    }

    public async Task<IActionResult> Index(string? q, int? categoryId, int page = 1)
    {
        var query = _db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.CreatedByUser).AsQueryable();
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term) || (p.Material != null && p.Material.Contains(term)));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new PanelProductListViewModel
        {
            Items = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Categories = await LoadCategoriesAsync(),
            Query = q,
            CategoryId = categoryId,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
        => View("Form", new ProductFormViewModel { Categories = await LoadCategoriesAsync() });

    [HttpPost]
    public async Task<IActionResult> Create(ProductFormViewModel model)
    {
        model.Categories = await LoadCategoriesAsync();
        if (!await CategoryExistsAsync(model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Geçerli bir kategori seçin.");

        var saved = new List<string>();
        string? imageUrl = null;
        if (model.Image is not null && model.Image.Length > 0)
        {
            var (url, error) = await _images.SaveImageAsync(model.Image);
            if (error is not null) ModelState.AddModelError(nameof(model.Image), error);
            else { imageUrl = url; saved.Add(url!); }
        }

        var galleryUrls = await SaveGalleryAsync(model, MaxGalleryImages, saved);

        if (!ModelState.IsValid)
        {
            foreach (var u in saved) _images.DeleteIfLocal(u);
            return View("Form", model);
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Description = model.Description.Trim(),
            Material = model.Material?.Trim(),
            Dimensions = model.Dimensions?.Trim(),
            Price = model.Price.HasValue ? Math.Round(model.Price.Value) : null,
            CategoryId = model.CategoryId,
            IsActive = model.IsActive,
            ImageUrl = imageUrl,
            CreatedByUserId = User.GetUserId(),
            Images = galleryUrls.Select((u, i) => new ProductImage { Url = u, SortOrder = i }).ToList()
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.ProductCreated, "Ürün", product.Id, $"Ürün eklendi: {product.Name}");

        TempData["Success"] = "Ürün eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Products.AsNoTracking().Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        return View("Form", new ProductFormViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Material = p.Material,
            Dimensions = p.Dimensions,
            Price = p.Price.HasValue ? Math.Round(p.Price.Value) : null,
            CategoryId = p.CategoryId,
            IsActive = p.IsActive,
            ExistingImageUrl = p.ImageUrl,
            ExistingGallery = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList(),
            Categories = await LoadCategoriesAsync()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
    {
        var product = await _db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id);
        if (product is null) return NotFound();

        model.Id = id;
        model.ExistingImageUrl = product.ImageUrl;
        model.ExistingGallery = product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        model.Categories = await LoadCategoriesAsync();
        if (!await CategoryExistsAsync(model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Geçerli bir kategori seçin.");

        var removedImages = product.Images.Where(i => model.RemoveImageIds.Contains(i.Id)).ToList();
        var saved = new List<string>();
        string? newImage = null;
        if (model.Image is not null && model.Image.Length > 0)
        {
            var (url, error) = await _images.SaveImageAsync(model.Image);
            if (error is not null) ModelState.AddModelError(nameof(model.Image), error);
            else { newImage = url; saved.Add(url!); }
        }

        var room = MaxGalleryImages - (product.Images.Count - removedImages.Count);
        var galleryUrls = await SaveGalleryAsync(model, room, saved);

        if (!ModelState.IsValid)
        {
            foreach (var u in saved) _images.DeleteIfLocal(u);
            return View("Form", model);
        }

        product.Name = model.Name.Trim();
        product.Description = model.Description.Trim();
        product.Material = model.Material?.Trim();
        product.Dimensions = model.Dimensions?.Trim();
        product.Price = model.Price.HasValue ? Math.Round(model.Price.Value) : null;
        product.CategoryId = model.CategoryId;
        product.IsActive = model.IsActive;

        if (newImage is not null || model.RemoveMainImage)
        {
            _images.DeleteIfLocal(product.ImageUrl);
            product.ImageUrl = newImage;
        }

        foreach (var img in removedImages)
        {
            _images.DeleteIfLocal(img.Url);
            product.Images.Remove(img);
            _db.ProductImages.Remove(img);
        }

        var nextOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1;
        foreach (var url in galleryUrls)
            product.Images.Add(new ProductImage { Url = url, SortOrder = nextOrder++ });

        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.ProductUpdated, "Ürün", product.Id, $"Ürün güncellendi: {product.Name}");

        TempData["Success"] = "Ürün güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        product.IsActive = !product.IsActive;
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.ProductToggled, "Ürün", product.Id,
            $"'{product.Name}' ürünü {(product.IsActive ? "yayına alındı" : "yayından kaldırıldı")}.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        var urls = product.Images.Select(i => i.Url).Append(product.ImageUrl).ToList();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        foreach (var u in urls) _images.DeleteIfLocal(u);
        await _log.LogAsync(LogActions.ProductDeleted, "Ürün", id, $"Ürün kalıcı olarak silindi: {product.Name}");

        TempData["Success"] = "Ürün silindi.";
        return RedirectToAction(nameof(Index));
    }

    // Galeri dosyalarını kaydeder; hatada ModelState'e yazar. Kaydedilen her url 'saved' listesine de eklenir (geri alma için).
    private async Task<List<string>> SaveGalleryAsync(ProductFormViewModel model, int room, List<string> saved)
    {
        var urls = new List<string>();
        var files = model.GalleryImages?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
        if (files.Count == 0) return urls;

        if (files.Count > Math.Max(room, 0))
        {
            ModelState.AddModelError(nameof(model.GalleryImages), $"Galeride en fazla {MaxGalleryImages} görsel olabilir.");
            return urls;
        }

        foreach (var file in files)
        {
            var (url, error) = await _images.SaveImageAsync(file);
            if (error is not null) { ModelState.AddModelError(nameof(model.GalleryImages), error); break; }
            urls.Add(url!);
            saved.Add(url!);
        }
        return urls;
    }

    private Task<List<Category>> LoadCategoriesAsync() => _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
    private Task<bool> CategoryExistsAsync(int id) => _db.Categories.AnyAsync(c => c.Id == id);
}
