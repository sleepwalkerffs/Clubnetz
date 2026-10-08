using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubAnnouncementEntityConfiguration : IEntityTypeConfiguration<ClubAnnouncement>
{
    public void Configure(EntityTypeBuilder<ClubAnnouncement> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.CreatedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.Title).HasMaxLength(ClubAnnouncement.MaxTitleLength);
        builder.Property(x => x.Body).HasMaxLength(ClubAnnouncement.MaxBodyLength);

        builder.HasIndex(x => new { x.ClubId, x.IsPinned, x.PublishedAt });
    }
}
