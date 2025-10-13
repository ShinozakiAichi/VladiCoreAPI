using System;
using Microsoft.EntityFrameworkCore;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Contexts;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductPriceHistory> ProductPriceHistory => Set<ProductPriceHistory>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<ProductView> ProductViews => Set<ProductView>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

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
            .Property(p => p.OldPrice)
            .HasColumnType("decimal(12,2)");

        modelBuilder.Entity<ProductPriceHistory>()
            .Property(h => h.Price)
            .HasColumnType("decimal(12,2)");

        modelBuilder.Entity<OrderItem>()
            .Property(i => i.UnitPrice)
            .HasColumnType("decimal(12,2)");

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        modelBuilder.Entity<ProductReview>()
            .Property(r => r.Photos)
            .HasConversion(
                photos => System.Text.Json.JsonSerializer.Serialize(photos, jsonOptions),
                json => string.IsNullOrWhiteSpace(json)
                    ? Array.Empty<string>()
                    : System.Text.Json.JsonSerializer.Deserialize<string[]>(json, jsonOptions) ?? Array.Empty<string>())
            .HasColumnType("json");

        modelBuilder.Entity<ProductReview>()
            .Property(r => r.Rating)
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<ProductReview>()
            .HasIndex(r => new { r.ProductId, r.IsApproved, r.CreatedAt })
            .HasDatabaseName("IX_ProductReviews_Product_IsApproved_CreatedAt");

        modelBuilder.Entity<ProductImage>()
            .HasIndex(i => new { i.ProductId, i.CreatedAt })
            .HasDatabaseName("IX_ProductImages_Product_CreatedAt");
    }
}
