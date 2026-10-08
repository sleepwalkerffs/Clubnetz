using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class BadgeTierImageEntityConfiguration : IEntityTypeConfiguration<BadgeTierImage>
{
    public void Configure(EntityTypeBuilder<BadgeTierImage> builder)
    {
        builder.HasKey(x => x.BadgeTierId);

        builder.HasOne<BadgeTier>()
            .WithOne()
            .HasForeignKey<BadgeTierImage>(x => x.BadgeTierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
