using Bookennis.Domain.Bookings;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class BookingPlayerConfiguration : IEntityTypeConfiguration<BookingPlayer>
{
    public void Configure(EntityTypeBuilder<BookingPlayer> builder)
    {
        builder.HasOne(n => n.Booking)
               .WithMany(n => n.Players)
               .HasForeignKey(n => n.BookingEntryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ClubMember>()
               .WithMany()
               .HasForeignKey(n => n.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.BookingEntryId, n.MemberId });
    }
}