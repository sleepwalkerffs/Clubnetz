using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class PushSubscriptionEntityConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.Property(x => x.Endpoint).HasMaxLength(PushSubscription.MaxEndpointLength);
        builder.Property(x => x.P256dh).HasMaxLength(PushSubscription.MaxKeyLength);
        builder.Property(x => x.Auth).HasMaxLength(PushSubscription.MaxKeyLength);

        // One row per device: the endpoint moves to whoever signed in on it last
        builder.HasIndex(x => x.Endpoint).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
