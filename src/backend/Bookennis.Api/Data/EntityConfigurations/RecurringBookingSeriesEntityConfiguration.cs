using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class RecurringBookingSeriesEntityConfiguration : IEntityTypeConfiguration<RecurringBookingSeries>
{
    public void Configure(EntityTypeBuilder<RecurringBookingSeries> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<PlayMode>()
               .WithMany()
               .HasForeignKey(n => n.PlayModeId);

        builder.HasOne<Court>()
               .WithMany()
               .HasForeignKey(n => n.CourtId);
    }
}
