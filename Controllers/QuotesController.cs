using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services;

namespace web.Controllers;

[Authorize]
public class QuotesController : Controller
{
    private const int MaxAttachments = 5;
    private readonly AppDbContext _db;
    private readonly IQuoteAnalyzer _analyzer;
    private readonly IActivityLogger _log;
    private readonly IImageStorage _images;

    public QuotesController(AppDbContext db, IQuoteAnalyzer analyzer, IActivityLogger log, IImageStorage images)
    {
        _db = db;
        _analyzer = analyzer;
        _log = log;
        _images = images;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? productId)
    {
        var vm = new QuoteCreateViewModel();
        if (productId.HasValue)
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
            if (product is not null)
            {
                vm.ProductId = product.Id;
                vm.ProductName = product.Name;
                vm.Title = $"{product.Name} için teklif talebi";
            }
        }
        return View(vm);
    }

    [HttpPost, EnableRateLimiting("quote")]
    public async Task<IActionResult> Create(QuoteCreateViewModel model)
    {
        Product? product = null;
        if (model.ProductId.HasValue)
        {
            product = await _db.Products.Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == model.ProductId && p.IsActive);
            model.ProductName = product?.Name;
        }

        var files = model.Attachments?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
        if (files.Count > MaxAttachments)
            ModelState.AddModelError(nameof(model.Attachments), $"En fazla {MaxAttachments} fotoğraf yükleyebilirsiniz.");

        var savedUrls = new List<string>();
        if (ModelState.IsValid)
        {
            foreach (var file in files)
            {
                var (url, error) = await _images.SaveImageAsync(file, isPrivate: true);
                if (error is not null) { ModelState.AddModelError(nameof(model.Attachments), error); break; }
                savedUrls.Add(url!);
            }
        }

        if (!ModelState.IsValid)
        {
            foreach (var u in savedUrls) _images.DeleteIfLocal(u);
            return View(model);
        }

        var analysis = await _analyzer.AnalyzeAsync(new QuoteAnalysisInput(
            model.Title, model.Description, model.CustomDimensions, product?.Category?.Name));

        var quote = new QuoteRequest
        {
            UserId = User.GetUserId()!.Value,
            ProductId = product?.Id,
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            CustomDimensions = model.CustomDimensions?.Trim(),
            Budget = model.Budget,
            Status = QuoteStatus.Pending,
            AiCategory = analysis.Category,
            AiPriority = analysis.Priority,
            AiEstimatedDays = analysis.EstimatedDays,
            AiSummary = analysis.Summary,
            AiKeywords = analysis.Keywords,
            Attachments = savedUrls.Select(u => new QuoteAttachment { Url = u }).ToList(),
            History = new List<QuoteStatusHistory>
            {
                new() { Status = QuoteStatus.Pending, Note = "Talep oluşturuldu.", ChangedBy = User.Identity?.Name ?? "" }
            }
        };
        _db.QuoteRequests.Add(quote);
        await _db.SaveChangesAsync();

        await _log.LogAsync(LogActions.QuoteCreated, "Teklif", quote.Id, $"Teklif #{quote.Id} oluşturuldu: {quote.Title} (AI: {analysis.Category}, {analysis.Priority})");

        TempData["Success"] = "Talebiniz alındı. Durumunu profilinizden takip edebilirsiniz.";
        return RedirectToAction(nameof(Details), new { id = quote.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var quote = await _db.QuoteRequests
            .AsNoTracking()
            .Include(q => q.Product)
            .Include(q => q.Attachments)
            .Include(q => q.History)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return NotFound();

        // Müşteri yalnızca kendi talebini görebilir.
        if (quote.UserId != User.GetUserId() && !User.IsStaff()) return Forbid();

        return View(quote);
    }

    // Müşterinin atölyenin verdiği fiyat teklifine yanıtı: onay / revize / ret.
    [HttpPost]
    public async Task<IActionResult> Respond(int id, CustomerResponse response, string? note)
    {
        if (response is not (CustomerResponse.Approved or CustomerResponse.RevisionRequested or CustomerResponse.Rejected))
            return BadRequest();

        var userId = User.GetUserId()!.Value;
        var quote = await _db.QuoteRequests.FirstOrDefaultAsync(q => q.Id == id && q.UserId == userId);
        if (quote is null) return NotFound();

        if (!quote.QuotedPrice.HasValue || quote.Status is QuoteStatus.Completed or QuoteStatus.Cancelled)
        {
            TempData["Error"] = "Bu talep için yanıt verilemez.";
            return RedirectToAction(nameof(Details), new { id });
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > 500 }) note = note[..500];

        quote.CustomerResponse = response;
        quote.CustomerResponseNote = note;
        quote.CustomerRespondedAt = DateTime.UtcNow;
        quote.UpdatedAt = DateTime.UtcNow;
        if (response == CustomerResponse.Rejected) quote.Status = QuoteStatus.Cancelled;

        var label = response switch
        {
            CustomerResponse.Approved => "Teklifi onayladı",
            CustomerResponse.RevisionRequested => "Revize istedi",
            _ => "Teklifi reddetti"
        };
        _db.QuoteStatusHistories.Add(new QuoteStatusHistory
        {
            QuoteRequestId = quote.Id,
            Status = quote.Status,
            Note = $"Müşteri: {label}." + (note is null ? "" : $" \"{note}\""),
            ChangedBy = User.Identity?.Name ?? ""
        });
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.QuoteCustomerResponse, "Teklif", quote.Id, $"Teklif #{quote.Id}: müşteri {label.ToLowerInvariant()}.");

        TempData["Success"] = "Yanıtınız atölyeye iletildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.GetUserId()!.Value;
        var quote = await _db.QuoteRequests.FirstOrDefaultAsync(q => q.Id == id && q.UserId == userId);
        if (quote is null) return NotFound();

        if (quote.Status != QuoteStatus.Pending)
        {
            TempData["Error"] = "Yalnızca inceleme aşamasındaki talepler iptal edilebilir.";
            return RedirectToAction(nameof(Details), new { id });
        }

        quote.Status = QuoteStatus.Cancelled;
        quote.UpdatedAt = DateTime.UtcNow;
        _db.QuoteStatusHistories.Add(new QuoteStatusHistory
        {
            QuoteRequestId = quote.Id,
            Status = QuoteStatus.Cancelled,
            Note = "Müşteri talebi iptal etti.",
            ChangedBy = User.Identity?.Name ?? ""
        });
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.QuoteCancelled, "Teklif", quote.Id, $"Müşteri teklif #{quote.Id} talebini iptal etti.");

        TempData["Success"] = "Talebiniz iptal edildi.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
