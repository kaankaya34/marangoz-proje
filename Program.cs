using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")!));

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
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

// Cloudflare Tunnel (cloudflared) aynı makineden HTTP ile bağlanır; HTTPS bilgisi X-Forwarded-* başlıklarıyla gelir.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

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
