using System.Text.Json;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class AccountingSettingsEntityConfiguration : IEntityTypeConfiguration<AccountingSettings>
{
    public void Configure(EntityTypeBuilder<AccountingSettings> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasIndex(x => x.ClubId).IsUnique();

        // The money accounts of the automatic entries
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.OnlineMoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.BankMoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.CashMoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.CardMoneyAccountId).OnDelete(DeleteBehavior.NoAction);
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

public class AccountingSourceSettingEntityConfiguration : IEntityTypeConfiguration<AccountingSourceSetting>
{
    public void Configure(EntityTypeBuilder<AccountingSourceSetting> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasOne<AccountingCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        builder.Ignore(x => x.IsOn);
        builder.HasIndex(x => new { x.ClubId, x.Source }).IsUnique();
    }
}

public class AccountingProposalEntityConfiguration : IEntityTypeConfiguration<AccountingProposal>
{
    public void Configure(EntityTypeBuilder<AccountingProposal> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.ToMoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<AccountingCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<AccountingEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.CounterpartyMemberId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.SourceKey).HasMaxLength(AccountingProposal.MaxSourceKeyLength);
        builder.Property(x => x.Text).HasMaxLength(AccountingEntry.MaxTextLength);
        builder.Property(x => x.CounterpartyName).HasMaxLength(AccountingEntry.MaxCounterpartyLength);
        builder.Property(x => x.Problem).HasMaxLength(AccountingProposal.MaxProblemLength);
        builder.Ignore(x => x.IsOpen);

        // A payment is suggested once
        builder.HasIndex(x => new { x.ClubId, x.SourceKey }).IsUnique();
        builder.HasIndex(x => new { x.ClubId, x.Status });
    }
}

public class BankStatementImportEntityConfiguration : IEntityTypeConfiguration<BankStatementImport>
{
    public void Configure(EntityTypeBuilder<BankStatementImport> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Member>().WithMany().HasForeignKey(x => x.ImportedByMemberId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.FileName).HasMaxLength(BankStatementImport.MaxFileNameLength);
        builder.HasIndex(x => new { x.ClubId, x.MoneyAccountId, x.ImportedAt });
    }
}

public class BankTransactionEntityConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<BankStatementImport>().WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<AccountingEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.CounterpartyName).HasMaxLength(BankTransaction.MaxNameLength);
        builder.Property(x => x.CounterpartyIban).HasMaxLength(34);
        builder.Property(x => x.Reference).HasMaxLength(BankTransaction.MaxReferenceLength);
        builder.Property(x => x.BankReference).HasMaxLength(BankTransaction.MaxBankReferenceLength);
        builder.Property(x => x.Fingerprint).HasMaxLength(BankTransaction.MaxFingerprintLength);
        builder.Ignore(x => x.IsOpen);

        // A line is imported once per money account, an entry books at most one line per money account (a transfer one on each side)
        builder.HasIndex(x => new { x.MoneyAccountId, x.Fingerprint }).IsUnique();
        builder.HasIndex(x => new { x.EntryId, x.MoneyAccountId }).IsUnique().HasFilter("\"EntryId\" IS NOT NULL");
        builder.HasIndex(x => new { x.ClubId, x.Status, x.BookingDate });
    }
}

public class BankRuleEntityConfiguration : IEntityTypeConfiguration<BankRule>
{
    public void Configure(EntityTypeBuilder<BankRule> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasOne<AccountingCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.MatchText).HasMaxLength(BankRule.MaxMatchTextLength);
        builder.Property(x => x.MatchIban).HasMaxLength(34);
        builder.Property(x => x.Text).HasMaxLength(AccountingEntry.MaxTextLength);
    }
}

public class BankCsvLayoutEntityConfiguration : IEntityTypeConfiguration<BankCsvLayout>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<BankCsvLayout> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.Columns)
               .HasColumnType("jsonb")
               .HasConversion(
                   columns => JsonSerializer.Serialize(columns, JsonOptions),
                   json => JsonSerializer.Deserialize<CsvColumns>(json, JsonOptions) ?? new CsvColumns(),
                   new ValueComparer<CsvColumns>(
                       (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                       c => JsonSerializer.Serialize(c, JsonOptions).GetHashCode(StringComparison.Ordinal),
                       c => c));
        builder.HasIndex(x => x.MoneyAccountId).IsUnique();
    }
}
