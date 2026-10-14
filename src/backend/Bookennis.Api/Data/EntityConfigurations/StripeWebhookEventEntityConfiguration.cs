using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class StripeWebhookEventEntityConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.Property(x => x.EventId).HasMaxLength(StripeWebhookEvent.MaxIdLength);
        builder.Property(x => x.Type).HasMaxLength(StripeWebhookEvent.MaxTypeLength);
        builder.Property(x => x.StripeAccountId).HasMaxLength(StripeWebhookEvent.MaxIdLength);
        builder.Property(x => x.ObjectId).HasMaxLength(StripeWebhookEvent.MaxIdLength);

        // An event that Stripe delivers twice is only stored (and handled) once
        builder.HasIndex(x => x.EventId).IsUnique();
    }
}
