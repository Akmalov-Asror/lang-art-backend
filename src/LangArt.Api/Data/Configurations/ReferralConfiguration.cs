using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(r => r.Status).IsRequired().HasDefaultValue("pending");
        b.Property(r => r.RewardLaDollars).HasDefaultValue(0);
        b.Property(r => r.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(r => r.ConvertedAt).HasColumnType("timestamptz");
        b.Property(r => r.RewardedAt).HasColumnType("timestamptz");
        b.HasIndex(r => r.ReferrerId);
        b.HasIndex(r => r.LeadId);
        b.HasIndex(r => r.RefereeProfileId);
    }
}
