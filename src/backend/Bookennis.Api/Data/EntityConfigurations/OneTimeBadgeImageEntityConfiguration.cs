using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class OneTimeBadgeImageEntityConfiguration : IEntityTypeConfiguration<OneTimeBadgeImage>
{
    public void Configure(EntityTypeBuilder<OneTimeBadgeImage> builder)
    {
        builder.HasKey(x => x.OneTimeBadgeId);

        builder.HasOne<OneTimeBadge>()
            .WithOne()
            .HasForeignKey<OneTimeBadgeImage>(x => x.OneTimeBadgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
