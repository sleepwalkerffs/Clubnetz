using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class BadgeTierEntityConfiguration : IEntityTypeConfiguration<BadgeTier>
{
    public void Configure(EntityTypeBuilder<BadgeTier> builder)
    {
        // Levels are numbered per track (sport, or every sport when null): two partial indexes instead of NULLS NOT DISTINCT
        builder.HasIndex(x => new { x.ClubId, x.SeasonId, x.Level })
            .IsUnique()
            .HasFilter("\"Sport\" IS NULL");

        builder.HasIndex(x => new { x.ClubId, x.SeasonId, x.Sport, x.Level })
            .IsUnique()
            .HasFilter("\"Sport\" IS NOT NULL");

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
