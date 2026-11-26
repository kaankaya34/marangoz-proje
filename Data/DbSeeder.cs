using Microsoft.EntityFrameworkCore;
using web.Models;
using web.Services;

namespace web.Data;

public static class DbSeeder
{
    // Veritabanında hiç Admin yoksa, yapılandırmadaki (Seed:*) bilgilerle ilk Admin (ve opsiyonel Yönetici) hesabını oluşturur.
    // Tablolar henüz oluşturulmadıysa uygulamanın çökmesini engellemek için hata loglanır.
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        // Örnek veri/admin oluşturma başarısız olsa bile müşteri dosyalarının gizlenmesi denenir.
        try
        {
            await MoveQuoteFilesToPrivateAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<IImageStorage>(), logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Teklif görselleri özel klasöre taşınamadı.");
        }

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>();

            await SeedSampleProductsAsync(db);

            if (await db.Users.AnyAsync(u => u.Role == Roles.Admin)) return;

            var email = cfg["Seed:AdminEmail"];
            var password = cfg["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Admin hesabı oluşturulmadı: Seed:AdminEmail / Seed:AdminPassword yapılandırılmamış.");
                return;
            }

            db.Users.Add(new AppUser
            {
                FullName = cfg["Seed:AdminName"] ?? "Sistem Yöneticisi",
                Email = email.Trim().ToLowerInvariant(),
                PasswordHash = hasher.Hash(password),
                Role = Roles.Admin
            });
            await db.SaveChangesAsync();
            logger.LogInformation("İlk Admin hesabı oluşturuldu: {Email}", email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Veritabanı başlangıç verisi oluşturulamadı. db.md içindeki SQL betiğinin çalıştırıldığından emin olun.");
        }
    }

    // Teklif görselleri herkese açık wwwroot/uploads altında kalmamalıdır: veritabanında teklif ekine bağlı olanlar ve
    // hiçbir ürüne bağlı olmayan (sahipsiz) dosyalar özel klasöre taşınır. Yalnızca ürün görselleri herkese açık kalır.
    private static async Task MoveQuoteFilesToPrivateAsync(AppDbContext db, IImageStorage images, ILogger logger)
    {
        // Boş/yanlış bir veritabanına bağlanıldığında ürün görselleri yanlışlıkla taşınmasın.
        if (!await db.Users.AnyAsync()) return;

        var moved = 0;

        var attachments = await db.QuoteAttachments.Where(a => a.Url.StartsWith("/uploads/")).ToListAsync();
        var history = await db.QuoteStatusHistories.Where(h => h.ImageUrl != null && h.ImageUrl.StartsWith("/uploads/")).ToListAsync();
        foreach (var a in attachments)
        {
            var url = images.MoveToPrivate(a.Url);
            if (url is not null) { a.Url = url; moved++; }
        }
        foreach (var h in history)
        {
            var url = images.MoveToPrivate(h.ImageUrl!);
            if (url is not null) { h.ImageUrl = url; moved++; }
        }
        await db.SaveChangesAsync();

        var productUrls = (await db.Products.Where(p => p.ImageUrl != null).Select(p => p.ImageUrl!).ToListAsync())
            .Concat(await db.ProductImages.Select(i => i.Url).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var url in images.ListPublicUploads())
        {
            if (productUrls.Contains(url)) continue;
            if (images.MoveToPrivate(url) is not null) moved++;
        }

        if (moved > 0) logger.LogInformation("{Count} teklif/sahipsiz görsel herkese açık klasörden özel klasöre taşındı.", moved);
    }

    // Vitrin boş görünmesin diye, hiç ürün yoksa örnek ürünleri (wwwroot/img/samples görselleriyle) ekler. Panelden silinebilir.
    private static async Task SeedSampleProductsAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync()) return;

        var categories = new (string Name, string Description)[]
        {
            ("Masa", "Yemek, çalışma ve sehpa çeşitleri"),
            ("Sandalye", "Yemek ve çalışma sandalyeleri"),
            ("Tabure", "Bar ve mutfak taburesi çeşitleri"),
            ("Sallanan Sandalye", "El yapımı sallanan sandalyeler")
        };
        var existing = await db.Categories.ToDictionaryAsync(c => c.Name);
        foreach (var (name, description) in categories)
        {
            if (existing.ContainsKey(name)) continue;
            var c = new Category { Name = name, Description = description };
            db.Categories.Add(c);
            existing[name] = c;
        }

        (string Cat, string Name, string Desc, string Material, string Dim, decimal Price, string Img)[] samples =
        {
            ("Masa", "Meşe Yemek Masası", "Masif meşeden, geçmeli birleşimlerle üretilen 6 kişilik yemek masası. Mat vernik yüzeyi lekeye ve çizilmeye karşı dayanıklıdır; damar deseni her parçada benzersizdir.", "Masif Meşe", "180 x 90 x 76 cm", 14500m, "mese-yemek"),
            ("Masa", "Ceviz Çalışma Masası", "Sade çizgili, iki çekmeceli ceviz çalışma masası. Kablo geçişi ve ölçüye göre yükseklik seçeneği sunulur; yağ cila ile ahşabın sıcak tonu korunur.", "Ceviz", "140 x 70 x 75 cm", 11800m, "mese-calisma"),
            ("Sandalye", "Klasik Kayın Sandalye", "Üç çıtalı sırtlığı ve sağlam ayak yapısıyla günlük kullanıma uygun klasik sandalye. Açık kayın rengi her mutfağa uyum sağlar.", "Kayın", "45 x 48 x 90 cm", 2450m, "sandalye-klasik"),
            ("Sandalye", "Ceviz Çubuklu Sandalye", "El oyması çubuk sırtlığı ve yuvarlatılmış oturma yüzeyiyle uzun oturumlarda bile rahat bir sandalye. Koyu ceviz tonunda yağ cila.", "Ceviz", "46 x 50 x 92 cm", 3100m, "sandalye-ceviz"),
            ("Tabure", "Meşe Bar Taburesi", "Ada tezgâhlar için 65 cm oturma yüksekliğinde, ayak desteği olan sağlam bar taburesi. İsteğe göre yükseklik ölçüye göre değiştirilir.", "Masif Meşe", "38 x 38 x 65 cm", 1750m, "tabure-bar"),
            ("Tabure", "Ihlamur Mutfak Taburesi", "Hafif ve istiflenebilir, ince işçilikli kısa mutfak taburesi. Doğal mat yüzey işlemi ile gelir.", "Ihlamur", "34 x 34 x 45 cm", 1200m, "tabure-mutfak"),
            ("Sallanan Sandalye", "Geleneksel Sallanan Sandalye", "Kiraz tonunda, yüksek sırtlıklı ve kolçaklı geleneksel sallanan sandalye. Dengeli kavisli ayakları sayesinde yumuşak ve sessiz sallanır.", "Kiraz", "65 x 95 x 100 cm", 6900m, "sallanan-geleneksel"),
            ("Sallanan Sandalye", "Modern Sallanan Koltuk", "Çıkarılabilir yastığı ve geniş oturma alanıyla modern çizgili sallanan koltuk. Kitap köşesi ve bebek odası için idealdir.", "Dişbudak", "68 x 98 x 102 cm", 8400m, "sallanan-modern")
        };

        var now = DateTime.UtcNow;
        for (var i = 0; i < samples.Length; i++)
        {
            var s = samples[i];
            db.Products.Add(new Product
            {
                Name = s.Name,
                Description = s.Desc,
                Material = s.Material,
                Dimensions = s.Dim,
                Price = s.Price,
                ImageUrl = $"/img/samples/{s.Img}.svg",
                Category = existing[s.Cat],
                CreatedAt = now.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();
    }
}
