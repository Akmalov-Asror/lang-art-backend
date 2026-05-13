using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(p => p.Email).IsRequired();
        b.HasIndex(p => p.Email).IsUnique();

        b.Property(p => p.PasswordHash).IsRequired();
        b.Property(p => p.FullName).IsRequired().HasDefaultValue(string.Empty);
        b.Property(p => p.IsActive).HasDefaultValue(true);
        b.Property(p => p.EmailVerified).HasDefaultValue(false);

        // `role` is a TEXT column with a CHECK constraint in the canonical schema,
        // NOT a Postgres native enum — map via value conversion to lowercase text.
        b.Property(p => p.Role)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => (Enums.Role)Enum.Parse(typeof(Enums.Role), v, true))
            .HasColumnType("text");

        b.Property(p => p.LastLogin).HasColumnType("timestamptz");
        b.Property(p => p.ResetTokenExpires).HasColumnType("timestamptz");
        b.Property(p => p.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(p => p.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}
