using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class GuestMemberEntityConfiguration : IEntityTypeConfiguration<GuestMember>
{
    public void Configure(EntityTypeBuilder<GuestMember> builder) => builder.HasBaseType<Member>();
}
