using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class UserProfilePictureEntityConfiguration : IEntityTypeConfiguration<UserProfilePicture>
{
    public void Configure(EntityTypeBuilder<UserProfilePicture> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserProfilePicture>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
