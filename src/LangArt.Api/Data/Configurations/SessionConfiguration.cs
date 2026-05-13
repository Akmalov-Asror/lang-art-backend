using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(s => s.RefreshToken).IsRequired();
        b.HasIndex(s => s.RefreshToken).IsUnique();

        b.Property(s => s.ExpiresAt).HasColumnType("timestamptz");
        b.Property(s => s.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(s => s.LastUsedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(s => s.User)
            .WithMany(p => p.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
