using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "core");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasColumnType("citext");
        builder.Property(x => x.Username).HasColumnType("citext");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Role)
               .WithMany()
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();
    }
}
