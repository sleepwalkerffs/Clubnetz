using Bookennis.Domain.ClubAnnouncements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubAnnouncementAttachmentEntityConfiguration : IEntityTypeConfiguration<ClubAnnouncementAttachment>
{
    public void Configure(EntityTypeBuilder<ClubAnnouncementAttachment> builder)
    {
        builder.HasOne(x => x.Announcement)
               .WithMany(x => x.Attachments)
               .HasForeignKey(x => x.ClubAnnouncementId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Content)
               .WithOne()
               .HasForeignKey<ClubAnnouncementAttachmentContent>(x => x.ClubAnnouncementAttachmentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.FileName).HasMaxLength(ClubAnnouncement.MaxFileNameLength);
        builder.Property(x => x.ContentType).HasMaxLength(100);
    }
}
