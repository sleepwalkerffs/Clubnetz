using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEventRegistrationEntityConfiguration : IEntityTypeConfiguration<ClubEventRegistration>
{
    public void Configure(EntityTypeBuilder<ClubEventRegistration> builder)
    {
        builder.HasOne(x => x.Event)
               .WithMany(x => x.Registrations)
               .HasForeignKey(x => x.ClubEventId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Comment).HasMaxLength(ClubEvent.MaxCommentLength);

        builder.HasIndex(x => new { x.ClubEventId, x.MemberId }).IsUnique();
    }
}
