using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MemberBadgeSettingsEntityConfiguration : IEntityTypeConfiguration<MemberBadgeSettings>
{
    public void Configure(EntityTypeBuilder<MemberBadgeSettings> builder)
    {
        builder.HasKey(x => x.MemberId);

        builder.HasOne<Member>()
            .WithOne()
            .HasForeignKey<MemberBadgeSettings>(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<MemberBadge>()
            .WithMany()
            .HasForeignKey(x => x.DisplayBadgeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<MemberOneTimeBadge>()
            .WithMany()
            .HasForeignKey(x => x.DisplayOneTimeBadgeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
