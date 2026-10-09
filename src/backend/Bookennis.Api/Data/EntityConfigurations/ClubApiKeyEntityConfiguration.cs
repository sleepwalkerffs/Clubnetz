using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubApiKeyEntityConfiguration : IEntityTypeConfiguration<ClubApiKey>
{
    public void Configure(EntityTypeBuilder<ClubApiKey> builder)
    {
        builder.HasIndex(x => x.KeyHash).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(ClubApiKey.NameMaxLength);
        builder.Property(x => x.KeyHash).HasMaxLength(64);
        builder.Property(x => x.KeyPrefix).HasMaxLength(16);

        builder.HasOne<Club>()
            .WithMany()
            .HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        // The key acts as its creator, so it is gone with the user
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
