using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MemberSeasonEntityConfiguration : IEntityTypeConfiguration<MemberSeason>
{
    public void Configure(EntityTypeBuilder<MemberSeason> builder)
    {
        builder.HasOne<Member>().WithMany().HasForeignKey(ms => ms.MemberId);
        builder.HasOne<Season>().WithMany().HasForeignKey(ms => ms.SeasonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(ms => new { ms.MemberId, ms.SeasonId }).IsUnique();
    }
}
