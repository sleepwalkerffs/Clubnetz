using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MembershipOrderMemberEntityConfiguration : IEntityTypeConfiguration<MembershipOrderMember>
{
    public void Configure(EntityTypeBuilder<MembershipOrderMember> builder)
    {
        builder.HasOne(x => x.Order)
               .WithMany(x => x.Members)
               .HasForeignKey(x => x.MembershipOrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.MembershipOrderId, x.MemberId }).IsUnique();
        builder.HasIndex(x => x.MemberId);
    }
}
