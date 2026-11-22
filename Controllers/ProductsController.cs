using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;

namespace web.Controllers;

public class ProductsController : Controller
{
    private const int PageSize = 9;
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, int? categoryId, string sort = "new", int page = 1)
    {
        var query = _db.Products.Include(p => p.Category).Where(p => p.IsActive);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term)
                                  || p.Description.Contains(term)
                                  || (p.Material != null && p.Material.Contains(term))
                                  || p.Category!.Name.Contains(term));
        }

        query = sort switch
        {
            "price_asc" => query.OrderBy(p => p.Price == null).ThenBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "name" => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var vm = new ProductListViewModel
        {
            Items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync(),
            Query = q,
            CategoryId = categoryId,
            Sort = sort,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product is null) return NotFound();

        ViewBag.Related = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != product.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(3)
            .ToListAsync();

        return View(product);
    }
}
