using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class SeasonEntityConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.OwnsDateOnlyInterval(n => n.Period);
        // Tenant relation (ClubId FK) is configured via ClubEntityConfiguration.HasMany(n => n.Seasons)
    }
}
