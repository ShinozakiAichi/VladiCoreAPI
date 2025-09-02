using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews", "catalog");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(r => new { r.ProductId, r.UserId }).IsUnique();
        builder.HasOne(r => r.Product)
               .WithMany(p => p.Reviews)
               .HasForeignKey(r => r.ProductId);
        builder.HasOne(r => r.User)
               .WithMany(u => u.Reviews)
               .HasForeignKey(r => r.UserId);
    }
}
