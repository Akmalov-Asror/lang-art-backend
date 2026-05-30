using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class UserLaDollarBalanceConfiguration : IEntityTypeConfiguration<UserLaDollarBalance>
{
    public void Configure(EntityTypeBuilder<UserLaDollarBalance> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.TotalBalance).HasDefaultValue(0);
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserLaDollarBalance>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LaDollarLedgerConfiguration : IEntityTypeConfiguration<LaDollarLedger>
{
    public void Configure(EntityTypeBuilder<LaDollarLedger> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Reason).IsRequired().HasColumnType("text");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        // Partial unique idempotency indexes created via raw DDL in SeedRunner.
    }
}

public class LaDollarStoreItemConfiguration : IEntityTypeConfiguration<LaDollarStoreItem>
{
    public void Configure(EntityTypeBuilder<LaDollarStoreItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Type).IsRequired();
        b.Property(x => x.Title).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}

public class LaDollarPurchaseConfiguration : IEntityTypeConfiguration<LaDollarPurchase>
{
    public void Configure(EntityTypeBuilder<LaDollarPurchase> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Status).IsRequired().HasDefaultValue("pending");
        b.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(x => x.FulfilledAt).HasColumnType("timestamptz");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.Status);
    }
}
