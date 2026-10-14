using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MembershipOrderEntityConfiguration : IEntityTypeConfiguration<MembershipOrder>
{
    public void Configure(EntityTypeBuilder<MembershipOrder> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Season>()
               .WithMany()
               .HasForeignKey(x => x.SeasonId)
               .OnDelete(DeleteBehavior.Cascade);

        // A plan with orders can not be deleted (DeleteMembershipPlan checks it). No action instead of restrict, so deleting
        // a season or a club can remove the plans and their orders in one statement.
        builder.HasOne<MembershipPlan>()
               .WithMany()
               .HasForeignKey(x => x.MembershipPlanId)
               .OnDelete(DeleteBehavior.NoAction);

        // The order is the club's record of a payment: it stays without the person when a member leaves
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.PayerMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MarkedPaidByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.PlanName).HasMaxLength(MembershipPlan.MaxNameLength);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Note).HasMaxLength(MembershipOrder.MaxNoteLength);
        builder.Property(x => x.StripeCheckoutSessionId).HasMaxLength(MembershipOrder.MaxStripeIdLength);
        builder.Property(x => x.StripePaymentIntentId).HasMaxLength(MembershipOrder.MaxStripeIdLength);

        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.IsFree);
        builder.Ignore(x => x.IsPayable);
        builder.Ignore(x => x.CanBeCancelledByMember);
        builder.Ignore(x => x.CanBeMarkedPaid);

        builder.HasIndex(x => new { x.ClubId, x.SeasonId, x.Status });
        builder.HasIndex(x => x.StripePaymentIntentId);
    }
}
