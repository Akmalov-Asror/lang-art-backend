using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(l => l.FullName).IsRequired();
        b.Property(l => l.Source).IsRequired().HasDefaultValue("other");
        b.Property(l => l.Stage).IsRequired().HasDefaultValue("new");
        b.Property(l => l.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(l => l.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(l => l.TrialAt).HasColumnType("timestamptz");
        b.Property(l => l.NextFollowUpAt).HasColumnType("timestamptz");

        b.HasIndex(l => l.Stage);
        b.HasIndex(l => l.AssignedTo);
        b.HasIndex(l => l.NextFollowUpAt);
    }
}

public class LeadActivityConfiguration : IEntityTypeConfiguration<LeadActivity>
{
    public void Configure(EntityTypeBuilder<LeadActivity> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(a => a.Kind).IsRequired().HasDefaultValue("note");
        b.Property(a => a.Body).IsRequired();
        b.Property(a => a.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(a => a.Lead)
            .WithMany(l => l.Activities)
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.LeadId, a.CreatedAt });
    }
}
