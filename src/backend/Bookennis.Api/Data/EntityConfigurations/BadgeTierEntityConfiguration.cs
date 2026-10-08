using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class BadgeTierEntityConfiguration : IEntityTypeConfiguration<BadgeTier>
{
    public void Configure(EntityTypeBuilder<BadgeTier> builder)
    {
        builder.HasIndex(x => new { x.ClubId, x.SeasonId, x.Level }).IsUnique();

        builder.HasOne<Club>()
            .WithMany()
            .HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(x => x.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
