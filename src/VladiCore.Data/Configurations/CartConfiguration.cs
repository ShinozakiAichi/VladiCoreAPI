using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts", "sales");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Currency).HasConversion<string>();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(c => c.SessionKey).IsUnique();
        builder.HasOne(c => c.User)
               .WithMany(u => u.Carts)
               .HasForeignKey(c => c.UserId);
    }
}
