using Bookennis.Domain.Families;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class FamilyEntityConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> builder)
    {
        builder.HasMany(n => n.Parents)
               .WithOne(n => n.ParentFamily)
               .HasForeignKey(n => n.ParentFamilyId);

        builder.Navigation(n => n.Parents).AutoInclude();

        builder.HasMany(n => n.Children)
               .WithOne(n => n.ChildFamily)
               .HasForeignKey(n => n.ChildFamilyId);

        builder.Navigation(n => n.Children).AutoInclude();

        builder.HasMany(n => n.FamilyMembers)
               .WithOne(n => n.Family)
               .HasForeignKey(n => n.FamilyId);

        builder.Navigation(n => n.FamilyMembers).AutoInclude();
    }
}