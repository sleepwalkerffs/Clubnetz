using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubhouseItemEntityConfiguration : IEntityTypeConfiguration<ClubhouseItem>
{
    public void Configure(EntityTypeBuilder<ClubhouseItem> builder)
    {
        builder.ConfigureTenantRelation();

        builder.Property(x => x.Name).HasMaxLength(ClubhouseItem.MaxNameLength);
        builder.Property(x => x.Currency).HasMaxLength(3);
    }
}

public class ClubhouseTabEntityConfiguration : IEntityTypeConfiguration<ClubhouseTab>
{
    public void Configure(EntityTypeBuilder<ClubhouseTab> builder)
    {
        builder.ConfigureTenantRelation();

        // The tab is the host's record of what was sold: it stays without the person when the member leaves
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.OpenedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.PaidToMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Lines)
               .WithOne(x => x.Tab)
               .HasForeignKey(x => x.ClubhouseTabId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.GuestName).HasMaxLength(ClubhouseTab.MaxGuestNameLength);
        builder.Property(x => x.Currency).HasMaxLength(3);

        builder.Ignore(x => x.IsOpen);
        builder.Ignore(x => x.TotalCents);

        // A member has one open tab per club, so the host always books onto the same one
        builder.HasIndex(x => new { x.ClubId, x.MemberId })
               .IsUnique()
               .HasFilter($"\"{nameof(ClubhouseTab.Status)}\" = {(int)ClubhouseTabStatus.Open} AND \"{nameof(ClubhouseTab.MemberId)}\" IS NOT NULL");
        builder.HasIndex(x => new { x.ClubId, x.Status, x.PaidAt });
    }
}

public class ClubhouseTabLineEntityConfiguration : IEntityTypeConfiguration<ClubhouseTabLine>
{
    public void Configure(EntityTypeBuilder<ClubhouseTabLine> builder)
    {
        // The line keeps name and price when the item is deleted
        builder.HasOne(x => x.Item)
               .WithMany()
               .HasForeignKey(x => x.ItemId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.AddedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.Name).HasMaxLength(ClubhouseItem.MaxNameLength);
        builder.Ignore(x => x.TotalCents);
    }
}
