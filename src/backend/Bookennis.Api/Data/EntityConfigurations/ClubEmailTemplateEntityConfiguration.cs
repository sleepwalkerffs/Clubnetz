using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.EmailTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubEmailTemplateEntityConfiguration : IEntityTypeConfiguration<ClubEmailTemplate>
{
    public void Configure(EntityTypeBuilder<ClubEmailTemplate> builder)
    {
        builder.HasIndex(x => new { x.ClubId, x.Type, x.Language }).IsUnique();
        builder.Property(x => x.Subject).HasMaxLength(ClubEmailTemplate.SubjectMaxLength);
        builder.Property(x => x.Body).HasMaxLength(ClubEmailTemplate.BodyMaxLength);

        builder.HasOne<Club>()
            .WithMany()
            .HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
