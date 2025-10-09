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
    }
}
