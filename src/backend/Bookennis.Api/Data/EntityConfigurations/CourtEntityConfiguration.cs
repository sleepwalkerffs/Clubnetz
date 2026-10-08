using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class CourtEntityConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ConfigureTenantRelation();
        builder.OwnsDateTimeOffsetInterval(n => n.Inactive);
    }
}