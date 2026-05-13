using LangArt.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(n => n.Kind).IsRequired();
        b.Property(n => n.Title).IsRequired();
        b.Property(n => n.ReadAt).HasColumnType("timestamptz");
        b.Property(n => n.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(n => n.UserId);
    }
}
