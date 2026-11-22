using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services;

namespace web.Areas.Panel.Controllers;

public class CategoriesController : PanelControllerBase
{
    private readonly AppDbContext _db;
    private readonly IActivityLogger _log;

    public CategoriesController(AppDbContext db, IActivityLogger log)
    {
        _db = db;
        _log = log;
    }

    public async Task<IActionResult> Index()
        => View(await _db.Categories.Include(c => c.Products).OrderBy(c => c.Name).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new Category());

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,Description")] Category model)
    {
        model.Name = model.Name?.Trim() ?? "";
        if (ModelState.IsValid && await _db.Categories.AnyAsync(c => c.Name == model.Name))
            ModelState.AddModelError(nameof(model.Name), "Bu isimde bir kategori zaten var.");

        if (!ModelState.IsValid) return View(model);

        _db.Categories.Add(model);
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.CategoryCreated, "Kategori", model.Id, $"Kategori eklendi: {model.Name}");

        TempData["Success"] = "Kategori eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Description")] Category model)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();

        model.Id = id;
        model.Name = model.Name?.Trim() ?? "";
        if (ModelState.IsValid && await _db.Categories.AnyAsync(c => c.Name == model.Name && c.Id != id))
            ModelState.AddModelError(nameof(model.Name), "Bu isimde bir kategori zaten var.");

        if (!ModelState.IsValid) return View(model);

        category.Name = model.Name;
        category.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.CategoryUpdated, "Kategori", id, $"Kategori güncellendi: {category.Name}");

        TempData["Success"] = "Kategori güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();

        if (category.Products.Count > 0)
        {
            TempData["Error"] = "İçinde ürün bulunan kategori silinemez.";
            return RedirectToAction(nameof(Index));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.CategoryDeleted, "Kategori", id, $"Kategori silindi: {category.Name}");

        TempData["Success"] = "Kategori silindi.";
        return RedirectToAction(nameof(Index));
    }
}
