using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class CourtBlockingOccurrenceEntityConfiguration : IEntityTypeConfiguration<CourtBlockingOccurrence>
{
    public void Configure(EntityTypeBuilder<CourtBlockingOccurrence> builder)
    {
        builder.HasOne(x => x.Blocking)
               .WithMany(x => x.Occurrences)
               .HasForeignKey(x => x.CourtBlockingId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsDateTimeOffsetInterval(x => x.Interval);
    }
}
