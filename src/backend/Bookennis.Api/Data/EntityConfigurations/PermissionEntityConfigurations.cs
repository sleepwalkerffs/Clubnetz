using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class RolePermissionEntityConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ConfigureTenantRelation();

        builder.Property(x => x.Permission).HasConversion<string>().HasMaxLength(60);
        builder.HasIndex(x => new { x.ClubId, x.Role, x.Permission }).IsUnique();
    }
}

public class PermissionGrantEntityConfiguration : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> builder)
    {
        builder.ConfigureTenantRelation();

        builder.Property(x => x.Permission).HasConversion<string>().HasMaxLength(60);
        builder.Property(x => x.ResourceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Effect).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Note).HasMaxLength(PermissionGrant.NoteMaxLength);

        // Personal data: gone with the member
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.GrantedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ClubId, x.MemberId });
    }
}
