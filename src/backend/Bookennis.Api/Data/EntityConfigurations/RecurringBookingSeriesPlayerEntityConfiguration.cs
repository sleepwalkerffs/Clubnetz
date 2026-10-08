using Bookennis.Domain.Bookings;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class RecurringBookingSeriesPlayerEntityConfiguration : IEntityTypeConfiguration<RecurringBookingSeriesPlayer>
{
    public void Configure(EntityTypeBuilder<RecurringBookingSeriesPlayer> builder)
    {
        builder.HasOne(n => n.Series)
               .WithMany(n => n.SeriesPlayers)
               .HasForeignKey(n => n.RecurringBookingSeriesId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ClubMember>()
               .WithMany()
               .HasForeignKey(n => n.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.RecurringBookingSeriesId, n.MemberId });
    }
}
