using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(x => x.Email).HasColumnType("citext").IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Role).HasConversion<string>();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
    }
}
