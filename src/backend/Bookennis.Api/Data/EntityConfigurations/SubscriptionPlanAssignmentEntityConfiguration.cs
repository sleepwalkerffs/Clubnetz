using Bookennis.Domain.SubscriptionPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class SubscriptionPlanAssignmentEntityConfiguration : IEntityTypeConfiguration<SubscriptionPlanAssignment>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlanAssignment> builder)
    {
        builder.HasOne(x => x.Plan)
               .WithMany(x => x.Assignments)
               .HasForeignKey(x => x.SubscriptionPlanId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Participant)
               .WithMany()
               .HasForeignKey(x => x.SubscriptionPlanParticipantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SubscriptionPlanId, x.WeekStart });
    }
}
