using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stock", "inventory");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasCheckConstraint("ck_stock_xor", "(product_id IS NOT NULL) <> (variant_id IS NOT NULL)");

        builder.HasOne(s => s.Product)
               .WithMany()
               .HasForeignKey(s => s.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Variant)
               .WithMany(v => v.Stocks)
               .HasForeignKey(s => s.VariantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.ProductId);
        builder.HasIndex(s => s.VariantId);
    }
}
