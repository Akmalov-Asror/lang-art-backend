using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(d => d.Kind).IsRequired().HasDefaultValue("public_offer");
        b.Property(d => d.Title).IsRequired();
        b.Property(d => d.BodyMarkdown).IsRequired();
        b.Property(d => d.IsCurrent).HasDefaultValue(true);
        b.Property(d => d.EffectiveFrom).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property(d => d.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.HasIndex(d => new { d.Kind, d.Version }).IsUnique();
        b.HasIndex(d => d.IsCurrent);
    }
}

public class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(a => a.Kind).IsRequired();
        b.Property(a => a.ContentHash).IsRequired();
        b.Property(a => a.AcceptedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.UserId, a.DocumentId }).IsUnique();
        b.HasIndex(a => new { a.UserId, a.Kind });
    }
}
