using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "sales");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>();
        builder.Property(o => o.Currency).HasConversion<string>();
        builder.Property(o => o.Subtotal).HasColumnType("numeric(12,2)");
        builder.Property(o => o.Shipping).HasColumnType("numeric(12,2)");
        builder.Property(o => o.Discount).HasColumnType("numeric(12,2)");
        builder.Property(o => o.Total).HasColumnType("numeric(12,2)");
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(o => o.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(o => o.User)
               .WithMany(u => u.Orders)
               .HasForeignKey(o => o.UserId);
        builder.HasOne(o => o.Cart)
               .WithOne()
               .HasForeignKey<Order>(o => o.CartId);
        builder.HasOne(o => o.ShippingAddress)
               .WithMany()
               .HasForeignKey(o => o.ShippingAddressId);
    }
}
