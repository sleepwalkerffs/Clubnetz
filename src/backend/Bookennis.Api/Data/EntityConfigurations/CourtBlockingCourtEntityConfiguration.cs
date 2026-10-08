using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class CourtBlockingCourtEntityConfiguration : IEntityTypeConfiguration<CourtBlockingCourt>
{
    public void Configure(EntityTypeBuilder<CourtBlockingCourt> builder)
    {
        builder.HasOne(x => x.Blocking)
               .WithMany(x => x.Courts)
               .HasForeignKey(x => x.CourtBlockingId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Court>()
               .WithMany()
               .HasForeignKey(x => x.CourtId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CourtBlockingId, x.CourtId }).IsUnique();
    }
}
