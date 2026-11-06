using Bookennis.Domain.Accounting;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class AccountingSettingsEntityConfiguration : IEntityTypeConfiguration<AccountingSettings>
{
    public void Configure(EntityTypeBuilder<AccountingSettings> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasIndex(x => x.ClubId).IsUnique();
    }
}

public class FiscalYearEntityConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ConfigureTenantRelation();

        // The records of the club: they stay without the person when the member leaves
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.ClosedByMemberId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.AuditedByMemberId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.OpeningBalances)
               .WithOne()
               .HasForeignKey(x => x.FiscalYearId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.OpeningBalances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.AuditComment).HasMaxLength(FiscalYear.MaxAuditCommentLength);
        builder.Ignore(x => x.IsOpen);
        builder.Ignore(x => x.Label);

        builder.HasIndex(x => new { x.ClubId, x.StartDate }).IsUnique();
    }
}

public class FiscalYearOpeningBalanceEntityConfiguration : IEntityTypeConfiguration<FiscalYearOpeningBalance>
{
    public void Configure(EntityTypeBuilder<FiscalYearOpeningBalance> builder)
    {
        builder.ToTable("FiscalYearOpeningBalances");
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.FiscalYearId, x.MoneyAccountId }).IsUnique();
    }
}

public class MoneyAccountEntityConfiguration : IEntityTypeConfiguration<MoneyAccount>
{
    public void Configure(EntityTypeBuilder<MoneyAccount> builder)
    {
        builder.ConfigureTenantRelation();
        builder.Property(x => x.Name).HasMaxLength(MoneyAccount.MaxNameLength);
        builder.Property(x => x.Iban).HasMaxLength(34);
        builder.HasIndex(x => new { x.ClubId, x.Name }).IsUnique();
    }
}

public class AccountingCategoryEntityConfiguration : IEntityTypeConfiguration<AccountingCategory>
{
    public void Configure(EntityTypeBuilder<AccountingCategory> builder)
    {
        builder.ConfigureTenantRelation();
        builder.Property(x => x.Name).HasMaxLength(AccountingCategory.MaxNameLength);
        builder.Property(x => x.Number).HasMaxLength(AccountingCategory.MaxNumberLength);
        builder.HasIndex(x => new { x.ClubId, x.Kind, x.Name }).IsUnique();
    }
}

public class CostCenterEntityConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ConfigureTenantRelation();
        builder.Property(x => x.Name).HasMaxLength(CostCenter.MaxNameLength);
        builder.HasIndex(x => new { x.ClubId, x.Name }).IsUnique();
    }
}

public class AccountingEntryEntityConfiguration : IEntityTypeConfiguration<AccountingEntry>
{
    public void Configure(EntityTypeBuilder<AccountingEntry> builder)
    {
        builder.ConfigureTenantRelation();

        // Entries are never deleted: what they point to can't be deleted while they use it (archive it instead).
        // NO ACTION, not RESTRICT: deleting the whole club removes everything in one statement
        builder.HasOne<FiscalYear>().WithMany().HasForeignKey(x => x.FiscalYearId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.ToMoneyAccountId).OnDelete(DeleteBehavior.NoAction);

        // The club's record: the name of the counterparty stays when the member leaves
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.CounterpartyMemberId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.CancelledByMemberId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Lines)
               .WithOne()
               .HasForeignKey(x => x.AccountingEntryId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Changes)
               .WithOne()
               .HasForeignKey(x => x.AccountingEntryId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Text).HasMaxLength(AccountingEntry.MaxTextLength);
        builder.Property(x => x.CounterpartyName).HasMaxLength(AccountingEntry.MaxCounterpartyLength);
        builder.Property(x => x.CancelReason).HasMaxLength(AccountingEntry.MaxCancelReasonLength);
        builder.Ignore(x => x.IsActive);

        // The number is the receipt number of the year: never twice
        builder.HasIndex(x => new { x.FiscalYearId, x.Number }).IsUnique();
        builder.HasIndex(x => new { x.ClubId, x.Date });
    }
}

public class AccountingEntryLineEntityConfiguration : IEntityTypeConfiguration<AccountingEntryLine>
{
    public void Configure(EntityTypeBuilder<AccountingEntryLine> builder)
    {
        builder.HasOne<AccountingCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class AccountingEntryChangeEntityConfiguration : IEntityTypeConfiguration<AccountingEntryChange>
{
    public void Configure(EntityTypeBuilder<AccountingEntryChange> builder)
    {
        builder.ToTable("AccountingEntryChanges");
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.ByMemberId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AccountingReceiptEntityConfiguration : IEntityTypeConfiguration<AccountingReceipt>
{
    public void Configure(EntityTypeBuilder<AccountingReceipt> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<AccountingEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.UploadedByMemberId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Content)
               .WithOne()
               .HasForeignKey<AccountingReceiptContent>(x => x.AccountingReceiptId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.FileName).HasMaxLength(AccountingReceipt.MaxFileNameLength);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Ignore(x => x.IsImage);

        builder.HasIndex(x => new { x.ClubId, x.EntryId });
    }
}

public class AccountingReceiptContentEntityConfiguration : IEntityTypeConfiguration<AccountingReceiptContent>
{
    public void Configure(EntityTypeBuilder<AccountingReceiptContent> builder)
    {
        builder.ToTable("AccountingReceiptContents");
        builder.HasKey(x => x.AccountingReceiptId);
    }
}
