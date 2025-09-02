using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", "catalog");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Slug).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(c => c.Parent)
               .WithMany(p => p.Children)
               .HasForeignKey(c => c.ParentId);
    }
}
