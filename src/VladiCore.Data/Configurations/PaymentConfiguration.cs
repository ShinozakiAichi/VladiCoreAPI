using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "sales");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasColumnType("numeric(12,2)");
        builder.Property(p => p.Currency).HasConversion<string>();
        builder.Property(p => p.Status).HasConversion<string>();
        builder.Property(p => p.Payload).HasColumnType("jsonb");
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(p => p.Order)
               .WithMany(o => o.Payments)
               .HasForeignKey(p => p.OrderId);
    }
}
