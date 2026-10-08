using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class BookingEntityConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ConfigureTenantRelation();
        builder.OwnsDateTimeOffsetInterval(n => n.Interval);

        builder.HasOne<PlayMode>()
               .WithMany()
               .HasForeignKey(n => n.PlayModeId);

        builder.HasOne<Court>()
               .WithMany()
               .HasForeignKey(n => n.CourtId);

        builder.HasOne(n => n.RecurringBookingSeries)
               .WithMany()
               .HasForeignKey(n => n.RecurringBookingSeriesId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
