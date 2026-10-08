using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class CourtBlockingEntityConfiguration : IEntityTypeConfiguration<CourtBlocking>
{
    public void Configure(EntityTypeBuilder<CourtBlocking> builder)
    {
        builder.ConfigureTenantRelation();

        builder.Property(x => x.Title).HasMaxLength(CourtBlocking.MaxTitleLength);

        builder.Ignore(x => x.IsAllDay);
        builder.Ignore(x => x.IsRecurring);
    }
}
