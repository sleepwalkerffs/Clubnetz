using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class MemberEntityConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(n => n.UserId);

        builder.HasDiscriminator(x => x.MemberType)
            .HasValue<ClubMember>(MemberType.ClubMember)
            .HasValue<GuestMember>(MemberType.GuestMember);

        builder.ConfigureTenantRelation();
    }
}