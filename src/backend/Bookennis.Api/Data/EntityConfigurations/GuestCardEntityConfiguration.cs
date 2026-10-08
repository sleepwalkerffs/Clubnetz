using Bookennis.Domain.Guests;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class GuestCardEntityConfiguration : IEntityTypeConfiguration<GuestCard>
{
    public void Configure(EntityTypeBuilder<GuestCard> builder)
    {
        builder.HasOne<GuestMember>()
            .WithMany()
            .HasForeignKey(x => x.GuestMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ConfigureTenantRelation();
    }
}
