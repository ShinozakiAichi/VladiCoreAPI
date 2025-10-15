using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VladiCore.Data.Identity;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Contexts;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductPriceHistory> ProductPriceHistory => Set<ProductPriceHistory>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<ProductView> ProductViews => Set<ProductView>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<ProductReviewVote> ProductReviewVotes => Set<ProductReviewVote>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<CoPurchase> CoPurchases => Set<CoPurchase>();

    public DbSet<Cpu> Cpus => Set<Cpu>();

    public DbSet<Motherboard> Motherboards => Set<Motherboard>();

    public DbSet<Ram> Rams => Set<Ram>();

    public DbSet<Gpu> Gpus => Set<Gpu>();

    public DbSet<Psu> Psus => Set<Psu>();

    public DbSet<Case> Cases => Set<Case>();

    public DbSet<Cooler> Coolers => Set<Cooler>();

    public DbSet<Storage> Storages => Set<Storage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>()
            .HasMany(c => c.Products)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasColumnType("decimal(12,2)");

        modelBuilder.Entity<Product>()
            .Property(p => p.AverageRating)
            .HasColumnType("decimal(3,2)");

        modelBuilder.Entity<Product>()
            .Property(p => p.Specs)
            .HasColumnType("json");

        modelBuilder.Entity<ProductPriceHistory>()
            .Property(h => h.Price)
            .HasColumnType("decimal(12,2)");

        modelBuilder.Entity<OrderItem>()
            .Property(i => i.UnitPrice)
            .HasColumnType("decimal(12,2)");

        modelBuilder.Entity<ProductReview>()
            .Property(r => r.Photos)
            .HasConversion(
                photos => System.Text.Json.JsonSerializer.Serialize(photos, SystemTextJsonOptions),
                json => string.IsNullOrWhiteSpace(json)
                    ? Array.Empty<string>()
                    : System.Text.Json.JsonSerializer.Deserialize<string[]>(json, SystemTextJsonOptions) ?? Array.Empty<string>())
            .HasColumnType("json");

        modelBuilder.Entity<ProductReview>()
            .Property(r => r.Rating)
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<ProductReview>()
            .Property(r => r.Status)
            .HasConversion(new EnumToStringConverter<ReviewStatus>())
            .HasMaxLength(32)
            .HasColumnType("varchar(32)");

        modelBuilder.Entity<ProductReview>()
            .HasIndex(r => new { r.ProductId, r.Status, r.CreatedAt })
            .HasDatabaseName("IX_ProductReviews_Product_Status_CreatedAt");

        modelBuilder.Entity<ProductReview>()
            .HasIndex(r => new { r.ProductId, r.Rating })
            .HasDatabaseName("IX_ProductReviews_Product_Rating");

        modelBuilder.Entity<ProductReview>()
            .HasIndex(r => new { r.UserId, r.ProductId })
            .HasDatabaseName("IX_ProductReviews_User_Product");

        modelBuilder.Entity<ProductReview>()
            .HasOne<ApplicationUser>()
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductReview>()
            .HasQueryFilter(r => !r.IsDeleted);

        modelBuilder.Entity<ProductReviewVote>()
            .HasKey(v => new { v.ReviewId, v.UserId });

        modelBuilder.Entity<ProductReviewVote>()
            .Property(v => v.Value)
            .HasColumnType("tinyint");

        modelBuilder.Entity<ProductReviewVote>()
            .HasOne(v => v.Review)
            .WithMany(r => r.Votes)
            .HasForeignKey(v => v.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasOne<ApplicationUser>()
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductImage>()
            .HasIndex(i => new { i.ProductId, i.SortOrder })
            .HasDatabaseName("IX_ProductImages_Product_SortOrder");

        modelBuilder.Entity<ProductImage>()
            .Property(i => i.SortOrder)
            .HasDefaultValue(0);

        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.CategoryId, p.Price })
            .HasDatabaseName("IX_Product_Category_Price");

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Name)
            .HasDatabaseName("IX_Product_Name");

        modelBuilder.Entity<CoPurchase>()
            .HasIndex(c => new { c.ProductId, c.WithProductId })
            .IsUnique();
    }

    private static readonly System.Text.Json.JsonSerializerOptions SystemTextJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
}
