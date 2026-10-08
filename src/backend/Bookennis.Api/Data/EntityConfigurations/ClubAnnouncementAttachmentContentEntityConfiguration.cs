using Bookennis.Domain.ClubAnnouncements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubAnnouncementAttachmentContentEntityConfiguration : IEntityTypeConfiguration<ClubAnnouncementAttachmentContent>
{
    public void Configure(EntityTypeBuilder<ClubAnnouncementAttachmentContent> builder)
    {
        builder.ToTable("ClubAnnouncementAttachmentContents");
        builder.HasKey(x => x.ClubAnnouncementAttachmentId);
    }
}
