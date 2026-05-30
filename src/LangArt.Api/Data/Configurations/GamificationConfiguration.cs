using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class UserXpConfiguration : IEntityTypeConfiguration<UserXp>
{
    public void Configure(EntityTypeBuilder<UserXp> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.TotalXp).HasDefaultValue(0);
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserXp>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserStreakConfiguration : IEntityTypeConfiguration<UserStreak>
{
    public void Configure(EntityTypeBuilder<UserStreak> b)
    {
        b.HasKey(s => s.UserId);
        b.Property(s => s.CurrentStreak).HasDefaultValue(0);
        b.Property(s => s.LongestStreak).HasDefaultValue(0);
        b.Property(s => s.LastActivityDateUtc).HasColumnType("date");

        b.HasOne(s => s.User)
            .WithOne()
            .HasForeignKey<UserStreak>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.Code).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).IsRequired();
        b.Property(x => x.Description).IsRequired().HasDefaultValue(string.Empty);
        b.Property(x => x.Criteria).HasColumnType("jsonb").HasDefaultValue("{}");
        b.Property(x => x.XpReward).HasDefaultValue(0);
        b.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}

public class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
{
    public void Configure(EntityTypeBuilder<UserBadge> b)
    {
        b.HasKey(ub => new { ub.UserId, ub.BadgeId });
        b.Property(ub => ub.EarnedAtUtc).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(ub => ub.User)
            .WithMany()
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(ub => ub.Badge)
            .WithMany(bd => bd.UserBadges)
            .HasForeignKey(ub => ub.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class XpLedgerConfiguration : IEntityTypeConfiguration<XpLedger>
{
    public void Configure(EntityTypeBuilder<XpLedger> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        // Reason is a TEXT column with a CHECK constraint (same pattern as `role`
        // and payments.status). Native PG enums are reserved for the schemas that
        // already had them at port-time (attendance_status, content_type).
        b.Property(x => x.Reason)
            .HasConversion(
                v => XpReasonNames.ToWire(v),
                v => XpReasonNames.FromWire(v))
            .HasColumnType("text");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hot-path index: "give me the most recent ledger rows for this user".
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        // Note: the two idempotency-guarding partial unique indexes are not
        // expressible cleanly via EF's HasIndex — they're created via raw DDL
        // in SeedRunner.EnsureSchemaUpgradesAsync.
    }
}
