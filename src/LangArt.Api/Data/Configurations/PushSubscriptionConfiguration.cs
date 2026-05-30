using LangArt.Api.Features.Notifications.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LangArt.Api.Data.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.ToTable("push_subscriptions");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(p => p.Endpoint).IsRequired();
        b.HasIndex(p => p.Endpoint).IsUnique();
        b.Property(p => p.P256dh).IsRequired();
        b.Property(p => p.Auth).IsRequired();
        b.Property(p => p.CreatedAtUtc).HasColumnType("timestamptz").HasDefaultValueSql("now()");

        b.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
