using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace web.Services;

public record QuoteAnalysisInput(string Title, string Description, string? Dimensions, string? ProductCategory);

public record QuoteAnalysis(
    string Category,
    string Priority,
    int EstimatedDays,
    string Summary,
    string Keywords);

/// <summary>
/// Özel imalat taleplerini analiz eden servis sözleşmesi.
/// Varsayılan uygulama (RuleBasedQuoteAnalyzer) kural/anahtar kelime tabanlıdır;
/// bir LLM servisine (Azure OpenAI vb.) geçmek için bu arayüzü uygulayıp Program.cs'de değiştirmek yeterlidir.
/// </summary>
public interface IQuoteAnalyzer
{
    Task<QuoteAnalysis> AnalyzeAsync(QuoteAnalysisInput input);
}

public class RuleBasedQuoteAnalyzer : IQuoteAnalyzer
{
    private static readonly (string Category, int BaseDays, Regex Pattern)[] Rules =
    {
        Rule("Sallanan Sandalye", 25, "sallanan", "salincak", "rocking", "rocker", "sallanir"),
        Rule("Tabure", 10, "tabure", "puf", "bar sandalyesi", "oturak"),
        Rule("Sandalye", 14, "sandalye", "koltuk"),
        Rule("Masa", 21, "masa", "sehpa", "konsol"),
    };

    private static (string, int, Regex) Rule(string category, int days, params string[] words)
        => (category, days, new Regex($@"\b(?:{string.Join("|", words.OrderByDescending(w => w.Length).Select(Regex.Escape))})\w*", RegexOptions.Compiled));

    private static readonly Dictionary<string, string> Woods = new()
    {
        ["mese"] = "Meşe", ["ceviz"] = "Ceviz", ["kayin"] = "Kayın", ["cam"] = "Çam", ["kestane"] = "Kestane", ["ladin"] = "Ladin",
        ["gurgen"] = "Gürgen", ["akasya"] = "Akasya", ["iroko"] = "İroko", ["fistik"] = "Fıstık", ["kavak"] = "Kavak", ["sedir"] = "Sedir"
    };
    private static readonly Dictionary<string, string> Finishes = new()
    {
        ["cila"] = "Cila", ["vernik"] = "Vernik", ["boya"] = "Boya", ["mat"] = "Mat", ["parlak"] = "Parlak", ["yag"] = "Yağ",
        ["lake"] = "Lake", ["naturel"] = "Naturel", ["antik"] = "Antik", ["eskitme"] = "Eskitme"
    };
    private static readonly string[] Urgent = { "acil", "hemen", "en kisa", "bu hafta", "yarin", "dugun", "tasinma", "ivedi" };
    private static readonly string[] Relaxed = { "acelesi yok", "acele yok", "vakit var", "uygun zamanda" };

    private static readonly Regex DimRegex = new(@"(\d{1,4}(?:[.,]\d+)?)\s*(?:x|×|\*)\s*(\d{1,4}(?:[.,]\d+)?)(?:\s*(?:x|×|\*)\s*(\d{1,4}(?:[.,]\d+)?))?\s*(cm|mm|m)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QtyRegex = new(@"(\d{1,3})\s*(adet|tane|takim|set)", RegexOptions.Compiled);

    public Task<QuoteAnalysis> AnalyzeAsync(QuoteAnalysisInput input)
    {
        var raw = $"{input.Title} {input.Description} {input.Dimensions}";
        var text = Normalize(raw);

        var category = "Diğer / Özel İmalat";
        var baseDays = 30;
        var best = Rules
            .Select((r, i) => (Rule: r, Index: i, Matches: r.Pattern.Matches(text)))
            .Where(x => x.Matches.Count > 0)
            // Sallanan sandalye "sandalye" kelimesini de içerdiğinden her zaman önceliklidir; diğerlerinde en çok geçen,
            // eşitlikte metinde ilk geçen kategori seçilir.
            .OrderBy(x => x.Index == 0 ? 0 : 1)
            .ThenByDescending(x => x.Matches.Count)
            .ThenBy(x => x.Matches[0].Index)
            .Select(x => ((string Category, int BaseDays, Regex Pattern)?)x.Rule)
            .FirstOrDefault();
        if (best is not null)
        {
            category = best.Value.Category;
            baseDays = best.Value.BaseDays;
        }

        if (category.StartsWith("Diğer") && !string.IsNullOrWhiteSpace(input.ProductCategory))
        {
            var hint = Rules.FirstOrDefault(r => Normalize(input.ProductCategory).Contains(Normalize(r.Category)));
            if (hint.Category is not null)
            {
                category = hint.Category;
                baseDays = hint.BaseDays;
            }
        }

        var priority = Urgent.Any(w => text.Contains(w)) ? "Yüksek"
                     : Relaxed.Any(w => text.Contains(w)) ? "Düşük"
                     : "Normal";

        var qty = 1;
        var qtyMatch = QtyRegex.Match(text);
        if (qtyMatch.Success && int.TryParse(qtyMatch.Groups[1].Value, out var q) && q > 0) qty = Math.Min(q, 100);

        var days = baseDays + (qty - 1) * 2;
        if (priority == "Yüksek") days = (int)Math.Ceiling(days * 0.8);

        var woods = Woods.Where(w => ContainsWord(text, w.Key)).Select(w => w.Value).ToList();
        var finishes = Finishes.Where(w => ContainsWord(text, w.Key)).Select(w => w.Value).ToList();

        var dimMatch = DimRegex.Match(raw);
        var dims = dimMatch.Success ? dimMatch.Value.Trim() : null;

        var keywords = woods.Concat(finishes).ToList();
        if (dims is not null) keywords.Add(dims);

        var summary = new StringBuilder($"{category} kategorisinde özel imalat talebi");
        if (qty > 1) summary.Append($", {qty} adet");
        if (woods.Count > 0) summary.Append($", ahşap: {string.Join("/", woods)}");
        if (finishes.Count > 0) summary.Append($", yüzey: {string.Join("/", finishes)}");
        if (dims is not null) summary.Append($", ölçü: {dims}");
        summary.Append($". Öncelik: {priority}. Tahmini üretim süresi ≈ {days} iş günü.");
        if (category.StartsWith("Diğer")) summary.Append(" Kategori otomatik belirlenemedi, manuel inceleme önerilir.");

        return Task.FromResult(new QuoteAnalysis(category, priority, days, summary.ToString(), string.Join(", ", keywords)));
    }

    private static bool ContainsWord(string text, string word)
        => Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\w*");

    private static string Normalize(string s)
    {
        var lower = s.ToLower(new CultureInfo("tr-TR"));
        var sb = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            sb.Append(c switch
            {
                'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                _ => c
            });
        }
        return sb.ToString();
    }
}
