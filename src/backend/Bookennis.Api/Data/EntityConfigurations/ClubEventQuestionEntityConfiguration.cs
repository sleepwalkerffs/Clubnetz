using Bookennis.Domain.ClubEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEventQuestionEntityConfiguration : IEntityTypeConfiguration<ClubEventQuestion>
{
    public void Configure(EntityTypeBuilder<ClubEventQuestion> builder)
    {
        builder.HasOne(x => x.Event)
               .WithMany(x => x.Questions)
               .HasForeignKey(x => x.ClubEventId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Text).HasMaxLength(ClubEvent.MaxQuestionTextLength);
        builder.Property(x => x.LimitQuantityToHeadCount).HasDefaultValue(true);
    }
}
