using System.Data.Entity;
using MySql.Data.EntityFramework;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Contexts
{
    [DbConfigurationType(typeof(MySqlEFConfiguration))]
    public class VladiCoreContext : DbContext
    {
        public VladiCoreContext(string connectionStringName = "MySql")
            : base($"name={connectionStringName}")
        {
            Configure();
        }

        public VladiCoreContext(string connectionString, bool useConnectionString)
            : base(useConnectionString ? connectionString : $"name={connectionString}")
        {
            Configure();
        }

        private void Configure()
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductPriceHistory> ProductPriceHistory { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ProductView> ProductViews { get; set; }
        public DbSet<Cpu> Cpus { get; set; }
        public DbSet<Motherboard> Motherboards { get; set; }
        public DbSet<Ram> Rams { get; set; }
        public DbSet<Gpu> Gpus { get; set; }
        public DbSet<Psu> Psus { get; set; }
        public DbSet<Domain.Entities.Case> Cases { get; set; }
        public DbSet<Cooler> Coolers { get; set; }
        public DbSet<Storage> Storages { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>()
                .HasMany(c => c.Products)
                .WithRequired(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.OldPrice)
                .HasPrecision(12, 2);

            modelBuilder.Entity<ProductPriceHistory>()
                .Property(h => h.Price)
                .HasPrecision(12, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(12, 2);
        }
    }
}
