using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using web.Data;
using web.Models;

namespace web.Areas.Panel.Controllers;

public class DashboardController : PanelControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var counts = await _db.QuoteRequests
            .GroupBy(q => q.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var vm = new DashboardViewModel
        {
            UserCount = await _db.Users.CountAsync(),
            ProductCount = await _db.Products.CountAsync(),
            CategoryCount = await _db.Categories.CountAsync(),
            QuoteCounts = Enum.GetValues<QuoteStatus>().ToDictionary(s => s, s => counts.GetValueOrDefault(s)),
            RecentQuotes = await _db.QuoteRequests
                .Include(q => q.User)
                .OrderByDescending(q => q.CreatedAt)
                .Take(8)
                .ToListAsync()
        };
        return View(vm);
    }
}
