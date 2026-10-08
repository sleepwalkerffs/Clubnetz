using Bookennis.Domain.SubscriptionPlans;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class SubscriptionPlanEntityConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(x => x.OwnerUserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Name).HasMaxLength(SubscriptionPlan.MaxNameLength);
        builder.Ignore(x => x.HasSchedule);

        builder.HasIndex(x => new { x.ClubId, x.OwnerUserId });
    }
}
