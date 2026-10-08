using Bookennis.Domain.ClubEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEventRegistrationAnswerEntityConfiguration : IEntityTypeConfiguration<ClubEventRegistrationAnswer>
{
    public void Configure(EntityTypeBuilder<ClubEventRegistrationAnswer> builder)
    {
        builder.HasOne(x => x.Registration)
               .WithMany(x => x.Answers)
               .HasForeignKey(x => x.ClubEventRegistrationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ClubEventQuestionOption>()
               .WithMany()
               .HasForeignKey(x => x.ClubEventQuestionOptionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ClubEventRegistrationId, x.ClubEventQuestionOptionId }).IsUnique();
    }
}
