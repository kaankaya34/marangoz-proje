using Microsoft.EntityFrameworkCore;
using web.Models;

namespace web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<QuoteRequest> QuoteRequests => Set<QuoteRequest>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<QuoteAttachment> QuoteAttachments => Set<QuoteAttachment>();
    public DbSet<QuoteStatusHistory> QuoteStatusHistories => Set<QuoteStatusHistory>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Category>().ToTable("Categories");

        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ProductImage>(e =>
        {
            e.ToTable("ProductImages");
            e.HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<QuoteAttachment>(e =>
        {
            e.ToTable("QuoteAttachments");
            e.HasOne(x => x.QuoteRequest).WithMany(q => q.Attachments).HasForeignKey(x => x.QuoteRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<QuoteStatusHistory>(e =>
        {
            e.ToTable("QuoteStatusHistories");
            e.HasOne(x => x.QuoteRequest).WithMany(q => q.History).HasForeignKey(x => x.QuoteRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SiteSetting>().ToTable("SiteSettings");

        b.Entity<QuoteRequest>(e =>
        {
            e.ToTable("QuoteRequests");
            e.Property(x => x.Budget).HasColumnType("decimal(18,2)");
            e.Property(x => x.QuotedPrice).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.User).WithMany(u => u.QuoteRequests).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Status);
        });

        b.Entity<ActivityLog>(e =>
        {
            e.ToTable("ActivityLogs");
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
