using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MemberOneTimeBadgeEntityConfiguration : IEntityTypeConfiguration<MemberOneTimeBadge>
{
    public void Configure(EntityTypeBuilder<MemberOneTimeBadge> builder)
    {
        builder.HasIndex(x => new { x.MemberId, x.OneTimeBadgeId }).IsUnique();

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OneTimeBadge>()
            .WithMany()
            .HasForeignKey(x => x.OneTimeBadgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
