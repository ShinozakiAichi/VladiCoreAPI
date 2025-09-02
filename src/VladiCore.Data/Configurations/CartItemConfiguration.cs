using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items", "sales");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("now()");

        builder.HasCheckConstraint("ck_cart_items_xor", "(product_id IS NOT NULL) <> (variant_id IS NOT NULL)");

        builder.HasOne(i => i.Cart)
               .WithMany(c => c.Items)
               .HasForeignKey(i => i.CartId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
               .WithMany()
               .HasForeignKey(i => i.ProductId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Variant)
               .WithMany()
               .HasForeignKey(i => i.VariantId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => new { i.CartId, i.ProductId })
               .IsUnique()
               .HasFilter("product_id IS NOT NULL");

        builder.HasIndex(i => new { i.CartId, i.VariantId })
               .IsUnique()
               .HasFilter("variant_id IS NOT NULL");
    }
}
