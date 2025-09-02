using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stock");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasCheckConstraint("CK_stock_product_variant", "(\"ProductId\" IS NOT NULL)::int + (\"VariantId\" IS NOT NULL)::int = 1");
        builder.HasOne(s => s.Product)
               .WithMany()
               .HasForeignKey(s => s.ProductId);
        builder.HasOne(s => s.Variant)
               .WithMany(v => v.Stocks)
               .HasForeignKey(s => s.VariantId);
    }
}
