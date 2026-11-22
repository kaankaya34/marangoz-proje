using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services;

namespace web.Areas.Panel.Controllers;

public class QuotesController : PanelControllerBase
{
    private const int PageSize = 15;
    private readonly AppDbContext _db;
    private readonly IActivityLogger _log;
    private readonly IQuoteAnalyzer _analyzer;
    private readonly IImageStorage _images;

    public QuotesController(AppDbContext db, IActivityLogger log, IQuoteAnalyzer analyzer, IImageStorage images)
    {
        _db = db;
        _log = log;
        _analyzer = analyzer;
        _images = images;
    }

    public async Task<IActionResult> Index(QuoteStatus? status, string? q, int page = 1)
    {
        var query = _db.QuoteRequests.Include(x => x.User).Include(x => x.Product).AsQueryable();

        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.Title.Contains(term) || x.User!.FullName.Contains(term) || x.User.Email.Contains(term));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new QuoteListViewModel
        {
            Items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Status = status,
            Query = q,
            Page = page,
            TotalPages = totalPages
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var quote = await _db.QuoteRequests
            .Include(x => x.User)
            .Include(x => x.Product)
            .Include(x => x.Attachments)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == id);
        return quote is null ? NotFound() : View(quote);
    }

    // Tek tıkla durum güncelleme: liste ve detay sayfasındaki butonlar bu aksiyona POST eder.
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, QuoteStatus status, string? returnUrl)
    {
        if (!Enum.IsDefined(status)) return BadRequest();

        var quote = await _db.QuoteRequests.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (quote is null) return NotFound();

        if (quote.Status != status)
        {
            var old = quote.Status;
            quote.Status = status;
            quote.UpdatedAt = DateTime.UtcNow;
            _db.QuoteStatusHistories.Add(new QuoteStatusHistory
            {
                QuoteRequestId = quote.Id,
                Status = status,
                ChangedBy = User.Identity?.Name ?? ""
            });
            await _db.SaveChangesAsync();

            await _log.LogAsync(LogActions.QuoteStatusUpdated, "Teklif", quote.Id,
                $"Teklif #{quote.Id} ({quote.User?.FullName}) durumu: '{old.Label()}' → '{status.Label()}'");
        }

        TempData["Success"] = $"Teklif #{id} durumu '{status.Label()}' olarak güncellendi.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // Net fiyat, tahmini teslim tarihi ve müşteriye görünen atölye notu.
    [HttpPost]
    public async Task<IActionResult> Offer(QuoteOfferViewModel model)
    {
        var quote = await _db.QuoteRequests.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == model.Id);
        if (quote is null) return NotFound();

        if (!ModelState.IsValid || !model.QuotedPrice.HasValue)
        {
            TempData["Error"] = "Geçerli bir net fiyat girin.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        var priceChanged = quote.QuotedPrice != model.QuotedPrice;
        quote.QuotedPrice = Math.Round(model.QuotedPrice.Value, 2);
        quote.EstimatedDeliveryDate = model.EstimatedDeliveryDate?.Date;
        quote.WorkshopNote = string.IsNullOrWhiteSpace(model.WorkshopNote) ? null : model.WorkshopNote.Trim();
        quote.UpdatedAt = DateTime.UtcNow;

        // Fiyat değiştiyse müşterinin önceki yanıtı geçersiz olur; yeniden yanıt vermesi gerekir.
        if (priceChanged)
        {
            quote.CustomerResponse = CustomerResponse.None;
            quote.CustomerResponseNote = null;
            quote.CustomerRespondedAt = null;
        }

        _db.QuoteStatusHistories.Add(new QuoteStatusHistory
        {
            QuoteRequestId = quote.Id,
            Status = quote.Status,
            Note = $"Atölye teklifi: {quote.QuotedPrice:N0} ₺" + (quote.EstimatedDeliveryDate.HasValue ? $", tahmini teslim {quote.EstimatedDeliveryDate:dd.MM.yyyy}." : "."),
            ChangedBy = User.Identity?.Name ?? ""
        });
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.QuotePriced, "Teklif", quote.Id, $"Teklif #{quote.Id} ({quote.User?.FullName}) için net fiyat verildi: {quote.QuotedPrice:N0} ₺");

        TempData["Success"] = "Fiyat teklifi müşteriye iletildi.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // Aşama güncellemesi: not ve isteğe bağlı atölye fotoğrafı müşterinin süreç geçmişinde görünür.
    [HttpPost]
    public async Task<IActionResult> AddProgress(QuoteProgressViewModel model)
    {
        if (!Enum.IsDefined(model.Status)) return BadRequest();

        var quote = await _db.QuoteRequests.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == model.Id);
        if (quote is null) return NotFound();

        var note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        if (note is { Length: > 500 })
        {
            TempData["Error"] = "Not en fazla 500 karakter olabilir.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        string? imageUrl = null;
        if (model.Image is { Length: > 0 })
        {
            var (url, error) = await _images.SaveImageAsync(model.Image);
            if (error is not null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            imageUrl = url;
        }

        if (note is null && imageUrl is null && quote.Status == model.Status)
        {
            TempData["Error"] = "Not, fotoğraf veya farklı bir aşama girin.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        quote.Status = model.Status;
        quote.UpdatedAt = DateTime.UtcNow;
        _db.QuoteStatusHistories.Add(new QuoteStatusHistory
        {
            QuoteRequestId = quote.Id,
            Status = model.Status,
            Note = note,
            ImageUrl = imageUrl,
            ChangedBy = User.Identity?.Name ?? ""
        });
        await _db.SaveChangesAsync();
        await _log.LogAsync(LogActions.QuoteNoted, "Teklif", quote.Id, $"Teklif #{quote.Id} ({quote.User?.FullName}) için aşama güncellemesi eklendi: {model.Status.Label()}");

        TempData["Success"] = "Aşama güncellemesi eklendi.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Reanalyze(int id)
    {
        var quote = await _db.QuoteRequests.Include(x => x.Product).ThenInclude(p => p!.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (quote is null) return NotFound();

        var a = await _analyzer.AnalyzeAsync(new QuoteAnalysisInput(quote.Title, quote.Description, quote.CustomDimensions, quote.Product?.Category?.Name));
        quote.AiCategory = a.Category;
        quote.AiPriority = a.Priority;
        quote.AiEstimatedDays = a.EstimatedDays;
        quote.AiSummary = a.Summary;
        quote.AiKeywords = a.Keywords;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Yapay zeka analizi yenilendi.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
