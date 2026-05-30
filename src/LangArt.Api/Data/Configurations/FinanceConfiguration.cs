using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class FinanceCategoryConfiguration : IEntityTypeConfiguration<FinanceCategory>
{
    public void Configure(EntityTypeBuilder<FinanceCategory> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(c => c.Name).IsRequired();
        b.Property(c => c.Kind).IsRequired();
        b.Property(c => c.IsActive).HasDefaultValue(true);
        b.Property(c => c.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasIndex(c => new { c.Kind, c.IsActive });
    }
}

public class FinanceTransactionConfiguration : IEntityTypeConfiguration<FinanceTransaction>
{
    public void Configure(EntityTypeBuilder<FinanceTransaction> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(t => t.OccurredOn).HasColumnType("date");
        b.Property(t => t.Amount).HasColumnType("numeric(14,2)");
        b.Property(t => t.Kind).IsRequired();
        b.Property(t => t.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(t => t.OccurredOn);
        b.HasIndex(t => new { t.Kind, t.OccurredOn });
        b.HasIndex(t => t.CategoryId);
    }
}
