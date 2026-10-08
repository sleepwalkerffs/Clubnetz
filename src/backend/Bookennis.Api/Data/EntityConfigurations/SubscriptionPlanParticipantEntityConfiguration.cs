using Bookennis.Domain.SubscriptionPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class SubscriptionPlanParticipantEntityConfiguration : IEntityTypeConfiguration<SubscriptionPlanParticipant>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlanParticipant> builder)
    {
        builder.HasOne(x => x.Plan)
               .WithMany(x => x.Participants)
               .HasForeignKey(x => x.SubscriptionPlanId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Name).HasMaxLength(SubscriptionPlan.MaxNameLength);
    }
}
