using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEventEntityConfiguration : IEntityTypeConfiguration<ClubEvent>
{
    public void Configure(EntityTypeBuilder<ClubEvent> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.CreatedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.Title).HasMaxLength(ClubEvent.MaxTitleLength);
        builder.Property(x => x.Description).HasMaxLength(ClubEvent.MaxDescriptionLength);
        builder.Property(x => x.Location).HasMaxLength(ClubEvent.MaxLocationLength);

        builder.Ignore(x => x.LastDate);
        builder.Ignore(x => x.TotalHeadCount);
        builder.Ignore(x => x.IsFull);

        builder.HasIndex(x => new { x.ClubId, x.StartDate });
    }
}
