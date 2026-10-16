using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class CourtSaleSettingsEntityConfiguration : IEntityTypeConfiguration<CourtSaleSettings>
{
    public void Configure(EntityTypeBuilder<CourtSaleSettings> builder)
    {
        builder.ConfigureTenantRelation();

        // A deleted play mode switches the sale off (the handlers check IsEnabled together with the play mode)
        builder.HasOne<PlayMode>()
               .WithMany()
               .HasForeignKey(x => x.PlayModeId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.TimeZoneId).HasMaxLength(CourtSaleSettings.MaxTimeZoneIdLength);
        builder.HasIndex(x => x.ClubId).IsUnique();
    }
}

public class CourtPriceRuleEntityConfiguration : IEntityTypeConfiguration<CourtPriceRule>
{
    public void Configure(EntityTypeBuilder<CourtPriceRule> builder)
    {
        builder.ConfigureTenantRelation();

        builder.Property(x => x.Name).HasMaxLength(CourtPriceRule.MaxNameLength);
        builder.Property(x => x.Currency).HasMaxLength(3);

        builder.Ignore(x => x.IsForAllCourts);
        builder.Ignore(x => x.StartMinute);
        builder.Ignore(x => x.EndMinute);
    }
}

public class CourtPurchaseEntityConfiguration : IEntityTypeConfiguration<CourtPurchase>
{
    public void Configure(EntityTypeBuilder<CourtPurchase> builder)
    {
        builder.ConfigureTenantRelation();
        builder.OwnsDateTimeOffsetInterval(x => x.Interval);

        // The purchase is the club's record of a payment: it stays without the person when the member leaves
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.CancelledByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        // ... and without the booking once the hold is released or the purchase cancelled
        builder.HasOne<Booking>()
               .WithMany()
               .HasForeignKey(x => x.BookingId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Court>()
               .WithMany()
               .HasForeignKey(x => x.CourtId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.StripeCheckoutSessionId).HasMaxLength(CourtPurchase.MaxStripeIdLength);
        builder.Property(x => x.StripePaymentIntentId).HasMaxLength(CourtPurchase.MaxStripeIdLength);

        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.HasOnlinePayment);

        builder.HasIndex(x => new { x.ClubId, x.Status });
        builder.HasIndex(x => x.StripePaymentIntentId);
        builder.HasIndex(x => x.StripeCheckoutSessionId);
    }
}

public class BookingCreditGrantEntityConfiguration : IEntityTypeConfiguration<BookingCreditGrant>
{
    public void Configure(EntityTypeBuilder<BookingCreditGrant> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.GrantedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.Note).HasMaxLength(BookingCreditGrant.MaxNoteLength);
    }
}
