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
        builder.Ignore(x => x.IsUnpaid);
        builder.Ignore(x => x.TotalCents);

        // A member has one unpaid tab per club (open, or locked while paying online), so the host always books onto the same one
        builder.HasIndex(x => new { x.ClubId, x.MemberId })
               .IsUnique()
               .HasFilter($"\"{nameof(ClubhouseTab.Status)}\" IN ({(int)ClubhouseTabStatus.Open}, {(int)ClubhouseTabStatus.AwaitingPayment}) AND \"{nameof(ClubhouseTab.MemberId)}\" IS NOT NULL");
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

public class ClubhouseTabPaymentEntityConfiguration : IEntityTypeConfiguration<ClubhouseTabPayment>
{
    public void Configure(EntityTypeBuilder<ClubhouseTabPayment> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<ClubhouseTab>()
               .WithMany()
               .HasForeignKey(x => x.TabId)
               .OnDelete(DeleteBehavior.Cascade);

        // The payment is the host's record of the money: it stays without the person when the member leaves
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.PayerMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.MerchantCode).HasMaxLength(ClubhouseTabPayment.MaxMerchantCodeLength);
        builder.Property(x => x.CheckoutId).HasMaxLength(ClubhouseTabPayment.MaxCheckoutIdLength);
        builder.Property(x => x.CheckoutUrl).HasMaxLength(ClubhouseTabPayment.MaxUrlLength);
        builder.Property(x => x.TransactionCode).HasMaxLength(ClubhouseTabPayment.MaxTransactionCodeLength);

        builder.Ignore(x => x.IsPending);
        builder.Ignore(x => x.CheckoutReference);

        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.CheckoutId).IsUnique();

        // One payment page per tab at a time: it is what locks the tab
        builder.HasIndex(x => x.TabId)
               .IsUnique()
               .HasFilter($"\"{nameof(ClubhouseTabPayment.Status)}\" = {(int)ClubhouseTabPaymentStatus.Pending}");
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}

public class ClubhouseSumUpAccountEntityConfiguration : IEntityTypeConfiguration<ClubhouseSumUpAccount>
{
    public void Configure(EntityTypeBuilder<ClubhouseSumUpAccount> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.ConnectedByMemberId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.MerchantCode).HasMaxLength(ClubhouseSumUpAccount.MaxMerchantCodeLength);
        builder.Property(x => x.MerchantName).HasMaxLength(ClubhouseSumUpAccount.MaxMerchantNameLength);

        builder.HasIndex(x => x.ClubId).IsUnique();
    }
}
