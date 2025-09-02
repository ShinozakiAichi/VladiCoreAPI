using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
        builder.HasCheckConstraint("CK_cartitem_product_variant", "(\"ProductId\" IS NOT NULL)::int + (\"VariantId\" IS NOT NULL)::int = 1");
        builder.HasOne(i => i.Cart)
               .WithMany(c => c.Items)
               .HasForeignKey(i => i.CartId);
        builder.HasIndex(i => new { i.CartId, i.ProductId, i.VariantId }).IsUnique();
    }
}
