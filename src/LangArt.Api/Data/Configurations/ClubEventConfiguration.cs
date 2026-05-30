using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class ClubEventConfiguration : IEntityTypeConfiguration<ClubEvent>
{
    public void Configure(EntityTypeBuilder<ClubEvent> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Title).IsRequired();
        b.Property(e => e.Kind).IsRequired().HasDefaultValue("club");
        b.Property(e => e.StartsAt).HasColumnType("timestamptz");
        b.Property(e => e.EndsAt).HasColumnType("timestamptz");
        b.Property(e => e.LaDollarReward).HasDefaultValue(0);
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.HasIndex(e => new { e.Kind, e.StartsAt });
        b.HasIndex(e => e.BranchId);
    }
}

public class ClubEventRsvpConfiguration : IEntityTypeConfiguration<ClubEventRsvp>
{
    public void Configure(EntityTypeBuilder<ClubEventRsvp> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(r => r.Status).IsRequired().HasDefaultValue("going");
        b.Property(r => r.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(r => r.Event)
            .WithMany(e => e.Rsvps)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(r => new { r.EventId, r.UserId }).IsUnique();
        b.HasIndex(r => r.UserId);
    }
}
