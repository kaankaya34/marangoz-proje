using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Antiforgery çerezi üretimde yalnızca HTTPS ile gönderilir (Cloudflare arkasında X-Forwarded-Proto ile HTTPS algılanır).
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// Şifreleme anahtarları diskte saklanır; yeniden başlatma/yayın sonrası oturum ve antiforgery çerezleri geçersiz kalmaz.
var keysPath = builder.Configuration["DataProtection:KeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
builder.Services.AddDataProtection()
    .SetApplicationName("ysmsandalye")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")!));

builder.Services.AddHttpContextAccessor();
// Her kayıt Size=1 ile eklenir; rastgele e-posta denemeleriyle bellek şişirilemez.
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.AddSingleton<ISiteSettings, SiteSettingsProvider>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IQuoteAnalyzer, RuleBasedQuoteAnalyzer>();
builder.Services.AddScoped<IActivityLogger, ActivityLogger>();
builder.Services.AddScoped<IImageStorage, ImageStorage>();
builder.Services.AddScoped<IUserSessionCache, UserSessionCache>();

// Giriş, kayıt ve teklif formları için IP başına istek sınırı.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync("Çok fazla istek gönderdiniz. Lütfen biraz bekleyip tekrar deneyin.", ct);
    };
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("quote", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.GetUserId()?.ToString() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Marangoz.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // Silinen kullanıcının veya rolü değişen kullanıcının eski çerezi geçersiz sayılır (60 sn önbellekli).
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var id = ctx.Principal?.GetUserId();
            var role = ctx.Principal?.FindFirst(ClaimTypes.Role)?.Value;
            var sessions = ctx.HttpContext.RequestServices.GetRequiredService<IUserSessionCache>();
            var valid = id.HasValue && role is not null && await sessions.IsValidAsync(id.Value, role);
            if (!valid)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync("Cookies");
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ManagerOrAdmin", p => p.RequireRole(web.Models.Roles.Manager, web.Models.Roles.Admin))
    .AddPolicy("AdminOnly", p => p.RequireRole(web.Models.Roles.Admin));

// Cloudflare Tunnel (cloudflared) aynı makineden (loopback) HTTP ile bağlanır. Yalnızca loopback'ten gelen başlıklara güvenilir,
// böylece istemci X-Forwarded-* başlığını taklit edemez. Gerçek istemci IP'si Cloudflare'ın CF-Connecting-IP başlığından okunur
// (hız sınırı ve giriş kilidi her ziyaretçiyi ayrı sayar). Başka bir ters vekil de güvenilecekse IP'leri ForwardedHeaders:KnownProxies altında verilebilir.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardedForHeaderName = "CF-Connecting-IP";
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPAddress.TryParse(proxy, out var ip)) options.KnownProxies.Add(ip);
    }
});

var app = builder.Build();

app.UseForwardedHeaders();

// Güvenlik başlıkları. CSP'de satır içi betikler yalnızca istek başına üretilen nonce ile çalışır.
app.Use(async (ctx, next) =>
{
    var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    ctx.Items["csp-nonce"] = nonce;

    ctx.Response.OnStarting(() =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        if (!app.Environment.IsDevelopment())
        {
            // Cloudflare Web Analytics betiği (static.cloudflareinsights.com) etkinse çalışabilsin diye izinlidir.
            h["Content-Security-Policy"] =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}' https://static.cloudflareinsights.com; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data:; " +
                "connect-src 'self' https://cloudflareinsights.com; " +
                "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
        }
        return Task.CompletedTask;
    });

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles(); // Çalışma zamanında yüklenen ürün görselleri (wwwroot/uploads) için gerekli
app.UseRouting();

// Fiyat/bütçe biçimi ve sayı ayrıştırma sunucu diline bağlı olmasın.
var trCulture = new CultureInfo("tr-TR");
var localization = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(trCulture),
    SupportedCultures = new List<CultureInfo> { trCulture },
    SupportedUICultures = new List<CultureInfo> { trCulture }
};
localization.RequestCultureProviders.Clear();
app.UseRequestLocalization(localization);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbSeeder.SeedAsync(app.Services);

app.Run();
