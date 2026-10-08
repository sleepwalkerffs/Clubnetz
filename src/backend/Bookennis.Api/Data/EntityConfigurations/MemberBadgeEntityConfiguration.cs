using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MemberBadgeEntityConfiguration : IEntityTypeConfiguration<MemberBadge>
{
    public void Configure(EntityTypeBuilder<MemberBadge> builder)
    {
        builder.HasIndex(x => new { x.MemberId, x.BadgeTierId, x.SeasonId }).IsUnique();

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BadgeTier>()
            .WithMany()
            .HasForeignKey(x => x.BadgeTierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(x => x.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
