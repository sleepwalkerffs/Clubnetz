using Bookennis.Domain.Clubs;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MembershipPlanEntityConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Season>()
               .WithMany()
               .HasForeignKey(x => x.SeasonId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Name).HasMaxLength(MembershipPlan.MaxNameLength);
        builder.Property(x => x.Description).HasMaxLength(MembershipPlan.MaxDescriptionLength);
        builder.Property(x => x.Currency).HasMaxLength(3);

        builder.Ignore(x => x.IsGroupPlan);
        builder.Ignore(x => x.IsFree);

        builder.HasIndex(x => new { x.ClubId, x.SeasonId, x.Name }).IsUnique();
    }
}
