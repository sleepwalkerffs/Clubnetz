using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubMemberEntityConfiguration : IEntityTypeConfiguration<ClubMember>
{
    public void Configure(EntityTypeBuilder<ClubMember> builder) => builder.HasBaseType<Member>();
}
