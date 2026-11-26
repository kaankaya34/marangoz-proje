using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;

namespace web.Controllers;

public class SeoController : Controller
{
    private readonly AppDbContext _db;

    public SeoController(AppDbContext db) => _db = db;

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urls = new List<(string Path, DateTime? LastMod)>
        {
            ("/", null),
            ("/Products", null),
            ("/Quotes/Create", null),
            ("/hakkimizda", null),
            ("/ahsap-rehberi", null),
            ("/iletisim", null),
            ("/sss", null),
            ("/kvkk", null)
        };

        var products = await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.CreatedAt })
            .ToListAsync();
        urls.AddRange(products.Select(p => ($"/Products/Details/{p.Id}", (DateTime?)p.CreatedAt)));

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                urls.Select(u => new XElement(ns + "url",
                    new XElement(ns + "loc", baseUrl + u.Path),
                    u.LastMod is { } d ? new XElement(ns + "lastmod", d.ToString("yyyy-MM-dd")) : null))));

        return Content(doc.Declaration + Environment.NewLine + doc.Root, "application/xml", Encoding.UTF8);
    }

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var text = $"User-agent: *\nDisallow: /Panel/\nDisallow: /Account/\n\nSitemap: {Request.Scheme}://{Request.Host}/sitemap.xml\n";
        return Content(text, "text/plain", Encoding.UTF8);
    }
}
