using Bookennis.Domain.ClubEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEventQuestionOptionEntityConfiguration : IEntityTypeConfiguration<ClubEventQuestionOption>
{
    public void Configure(EntityTypeBuilder<ClubEventQuestionOption> builder)
    {
        builder.HasOne(x => x.Question)
               .WithMany(x => x.Options)
               .HasForeignKey(x => x.ClubEventQuestionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Label).HasMaxLength(ClubEvent.MaxOptionLabelLength);
    }
}
