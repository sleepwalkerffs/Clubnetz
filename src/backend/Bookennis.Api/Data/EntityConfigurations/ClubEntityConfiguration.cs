using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEntityConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.HasMany(n => n.PlayModes).WithOne(i => i.Club);
        builder.HasMany(n => n.Seasons).WithOne().HasForeignKey(s => s.ClubId);
        builder.OwnsTimeOnlyInterval(n => n.OpeningHours);
        builder.OwnsOne(n => n.PrimeTimeSettings, ps =>
        {
            ps.Property(p => p.ApplicableWeekdays);
            ps.Property(p => p.ChildAgeThreshold);
            ps.OwnsTimeOnlyInterval(p => p.PrimeTimeHours);
        });
        builder.Property(n => n.WebsiteUrl).HasMaxLength(500);
        builder.Property(n => n.ReplyToEmail).HasMaxLength(256);
    }
}
