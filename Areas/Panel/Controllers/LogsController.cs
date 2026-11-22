using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;

namespace web.Areas.Panel.Controllers;

[Authorize(Policy = "AdminOnly")]
public class LogsController : PanelControllerBase
{
    private const int PageSize = 25;
    private readonly AppDbContext _db;

    public LogsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? logAction, string? q, int page = 1)
    {
        var action = logAction;
        var query = _db.ActivityLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(l => l.Action == action);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l => l.UserName.Contains(term) || l.Details.Contains(term));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new LogListViewModel
        {
            Items = await query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Action = action,
            Query = q,
            Page = page,
            TotalPages = totalPages,
            Actions = await _db.ActivityLogs.Select(l => l.Action).Distinct().OrderBy(a => a).ToListAsync()
        });
    }
}
