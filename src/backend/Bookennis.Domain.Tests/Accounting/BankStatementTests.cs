using System.Text;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Accounting;

public class BankStatementTests
{
    private static BankStatementReadResult Read(string text, CsvColumns? saved = null, Encoding? encoding = null)
        => BankStatementReader.Read((encoding ?? Encoding.UTF8).GetBytes(text), saved);

    private const string Camt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
          <BkToCstmrStmt>
            <Stmt>
              <Acct><Id><IBAN>AT611904300234573201</IBAN></Id></Acct>
              <Bal><Tp><CdOrPrtry><Cd>PRCD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">1000.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-04-30</Dt></Dt></Bal>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">1068.50</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-05-31</Dt></Dt></Bal>
              <Ntry>
                <Amt Ccy="EUR">120.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts>
                <BookgDt><Dt>2026-05-02</Dt></BookgDt><ValDt><Dt>2026-05-03</Dt></ValDt>
                <AcctSvcrRef>REF-1</AcctSvcrRef>
                <NtryDtls><TxDtls>
                  <Refs><EndToEndId>NOTPROVIDED</EndToEndId></Refs>
                  <RltdPties><Dbtr><Pty><Nm>Anna Huber</Nm></Pty></Dbtr><DbtrAcct><Id><IBAN>AT483200000012345864</IBAN></Id></DbtrAcct></RltdPties>
                  <RmtInf><Ustrd>Mitgliedsbeitrag 2026</Ustrd></RmtInf>
                </TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">51.50</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts>
                <BookgDt><Dt>2026-05-10</Dt></BookgDt>
                <NtryDtls>
                  <TxDtls><AmtDtls><TxAmt><Amt Ccy="EUR">30.00</Amt></TxAmt></AmtDtls><CdtDbtInd>DBIT</CdtDbtInd>
                    <RltdPties><Cdtr><Nm>ÖTV</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Lizenz</Ustrd></RmtInf></TxDtls>
                  <TxDtls><AmtDtls><TxAmt><Amt Ccy="EUR">21.50</Amt></TxAmt></AmtDtls><CdtDbtInd>DBIT</CdtDbtInd>
                    <RltdPties><Cdtr><Nm>Energie AG</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Strom</Ustrd></RmtInf></TxDtls>
                </NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">5.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts><Cd>PDNG</Cd></Sts><BookgDt><Dt>2026-05-31</Dt></BookgDt>
              </Ntry>
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;

    [Fact]
    public void Camt053_GivesBalancesAndBookedLines_AndSplitsBatches()
    {
        var result = Read(Camt);

        result.Format.Should().Be(BankStatementFormat.Camt053);
        var statement = result.Statement!;
        statement.Iban.Should().Be("AT611904300234573201");
        statement.OpeningBalanceCents.Should().Be(100_000);
        statement.ClosingBalanceCents.Should().Be(106_850);
        statement.ClosingDate.Should().Be(new DateOnly(2026, 5, 31));

        statement.Lines.Should().HaveCount(3, "the pending line is left out, the batch becomes two lines");
        var fee = statement.Lines[0];
        fee.AmountCents.Should().Be(12_000);
        fee.BookingDate.Should().Be(new DateOnly(2026, 5, 2));
        fee.ValueDate.Should().Be(new DateOnly(2026, 5, 3));
        fee.CounterpartyName.Should().Be("Anna Huber");
        fee.CounterpartyIban.Should().Be("AT483200000012345864");
        fee.Reference.Should().Be("Mitgliedsbeitrag 2026");
        fee.BankReference.Should().Be("REF-1");

        statement.Lines.Skip(1).Select(l => (l.AmountCents, l.CounterpartyName, l.Reference))
            .Should().Equal((-3_000L, "ÖTV", "Lizenz"), (-2_150L, "Energie AG", "Strom"));
    }

    [Fact]
    public void FileWithSeveralAccounts_IsReadForTheAccount_OrRefused()
    {
        const string twoAccounts = """
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"><BkToCstmrStmt>
              <Stmt><Acct><Id><IBAN>AT611904300234573201</IBAN></Id></Acct>
                <Ntry><Amt Ccy="EUR">10.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-05-02</Dt></BookgDt></Ntry></Stmt>
              <Stmt><Acct><Id><IBAN>AT483200000012345864</IBAN></Id></Acct>
                <Ntry><Amt Ccy="EUR">99.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-05-02</Dt></BookgDt></Ntry></Stmt>
            </BkToCstmrStmt></Document>
            """;

        var savings = BankStatementReader.Read(Encoding.UTF8.GetBytes(twoAccounts), null, accountIban: "AT48 3200 0000 1234 5864").Statement!;
        savings.Iban.Should().Be("AT483200000012345864");
        savings.Lines.Should().ContainSingle().Which.AmountCents.Should().Be(9_900);

        FluentActions.Invoking(() => Read(twoAccounts)).Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingBankFileSeveralAccounts));

        const string mt940 = ":20:A\n:25:AT611904300234573201\n:60F:C260430EUR0,00\n:61:2605020502C10,00NTRFNONREF\n:86:Giro\n:62F:C260502EUR10,00\n-\n"
                             + ":20:B\n:25:AT483200000012345864\n:60F:C260430EUR0,00\n:61:2605020502C99,00NTRFNONREF\n:86:Spar\n:61:2605020502C0,00NTRFNONREF\n:62F:C260502EUR99,00\n-\n";
        var spar = BankStatementReader.Read(Encoding.UTF8.GetBytes(mt940), null, accountIban: "AT483200000012345864").Statement!;
        spar.Lines.Should().ContainSingle("the line of 0.00 is no movement").Which.Reference.Should().Be("Spar");
        spar.ClosingBalanceCents.Should().Be(9_900);
    }

    [Fact]
    public void Mt940_ReadsStructuredAndFreeTextLines()
    {
        const string mt940 = """
            :20:STARTUMS
            :25:AT611904300234573201
            :28C:00005/001
            :60F:C260430EUR1000,00
            :61:2605020502CR120,00NTRFNONREF//BANKREF1
            :86:166?00GUTSCHRIFT?20Mitgliedsbeitrag ?212026 Huber?30BKAUATWW?31AT483200000012345864?32Anna Huber
            :61:2605100510DR30,00NMSCNONREF
            :86:Lastschrift OeTV Lizenz 2026
            :62F:C260531EUR1090,00
            -
            """;

        var statement = Read(mt940).Statement!;

        statement.OpeningBalanceCents.Should().Be(100_000);
        statement.ClosingBalanceCents.Should().Be(109_000);
        statement.ClosingDate.Should().Be(new DateOnly(2026, 5, 31));
        statement.Iban.Should().Be("AT611904300234573201");
        statement.Lines.Should().HaveCount(2);
        statement.Lines[0].Should().Be(new BankStatementLine(new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 2), 12_000, "Anna Huber", "AT483200000012345864", "Mitgliedsbeitrag 2026 Huber", "BANKREF1"));
        statement.Lines[1].AmountCents.Should().Be(-3_000);
        statement.Lines[1].Reference.Should().Be("Lastschrift OeTV Lizenz 2026");
    }

    [Fact]
    public void Csv_IsRecognizedByItsHeaders_AfterLinesAboutTheAccount()
    {
        const string csv = """
            Kontoinhaber;TC Vorderland
            IBAN;AT61 1904 3002 3457 3201
            ;
            Buchungsdatum;Valutadatum;Empfänger/Auftraggeber;IBAN;Verwendungszweck;Betrag;Währung
            02.05.2026;03.05.2026;Anna Huber;AT48 3200 0000 1234 5864;Mitgliedsbeitrag 2026;1.120,00;EUR
            10.05.2026;10.05.2026;"ÖTV; Wien";;Lizenz;-30,00;EUR
            """;

        var result = Read(csv, encoding: Encoding.Latin1);

        result.Format.Should().Be(BankStatementFormat.Csv);
        result.NeedsCsvLayout.Should().BeFalse();
        result.Statement!.Lines.Select(l => (l.BookingDate, l.AmountCents, l.CounterpartyName, l.Reference)).Should().Equal(
            (new DateOnly(2026, 5, 2), 112_000L, "Anna Huber", "Mitgliedsbeitrag 2026"),
            (new DateOnly(2026, 5, 10), -3_000L, "ÖTV; Wien", "Lizenz"));
        result.Statement.Lines[0].CounterpartyIban.Should().Be("AT48 3200 0000 1234 5864");
    }

    [Fact]
    public void Csv_WithDebitCreditMarkerOrColumns()
    {
        const string marker = "Buchungstag;Umsatz;S/H;Buchungstext\n02.05.2026;120,00;H;Beitrag\n03.05.2026;30,00;S;Lizenz\n";
        Read(marker).Statement!.Lines.Select(l => l.AmountCents).Should().Equal(12_000, -3_000);

        const string columns = "Datum,Soll,Haben,Text\n2026-05-02,,120.00,Beitrag\n2026-05-03,30.00,,Lizenz\n";
        Read(columns).Statement!.Lines.Select(l => l.AmountCents).Should().Equal(12_000, -3_000);
    }

    [Fact]
    public void Csv_WithoutHeader_LikeRaiffeisenElba()
    {
        const string elba = "02.05.2026;Gutschrift Huber Beitrag;03.05.2026;+120,00;EUR;02.05.2026 10:11:12\n10.05.2026;Lastschrift OeTV;10.05.2026;-30,00;EUR;10.05.2026 08:00:00\n";

        var lines = Read(elba).Statement!.Lines;

        lines.Select(l => (l.AmountCents, l.Reference)).Should().Equal((12_000L, "Gutschrift Huber Beitrag"), (-3_000L, "Lastschrift OeTV"));
    }

    [Fact]
    public void Csv_WithUnknownColumns_AsksForTheLayout_AndIsReadWithIt()
    {
        const string csv = "Tag|Wert|Partner|Zweck\n02.05.2026|120,00|Anna Huber|Beitrag\n03.05.2026|-30,00|ÖTV|Lizenz\n";

        var unknown = Read(csv);

        unknown.NeedsCsvLayout.Should().BeTrue();
        unknown.Preview!.Should().HaveCount(3);
        unknown.Preview![0].Should().Equal("Tag", "Wert", "Partner", "Zweck");
        unknown.DetectedColumns!.Delimiter.Should().Be('|');

        var layout = new CsvColumns { Delimiter = '|', SkipRows = 1, BookingDate = 0, Amount = 1, CounterpartyName = 2, Reference = [3] };
        var read = Read(csv, layout);
        read.NeedsCsvLayout.Should().BeFalse();
        read.Statement!.Lines.Select(l => (l.AmountCents, l.CounterpartyName)).Should().Equal((12_000L, "Anna Huber"), (-3_000L, "ÖTV"));
    }

    [Theory]
    [InlineData("1.234,56", 123_456)]
    [InlineData("-1.234,56", -123_456)]
    [InlineData("1,234.56", 123_456)]
    [InlineData("1234,5", 123_450)]
    [InlineData("12,00 S", -1_200)]
    [InlineData("12,00-", -1_200)]
    [InlineData("+12,00 EUR", 1_200)]
    [InlineData("€ 1.000", 100_000)]
    [InlineData("abc", null)]
    public void Csv_Amounts(string text, int? cents) => BankStatementReader.Csv.ParseAmount(text).Should().Be(cents);

    [Fact]
    public void NoStatement_IsRefused()
    {
        FluentActions.Invoking(() => Read("<html><body>no</body></html>")).Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingBankFileInvalid));
        FluentActions.Invoking(() => BankStatementReader.Read([], null)).Should().Throw<PreconditionException>();
    }
}

public class BankTransactionTests
{
    private const int ClubId = 7;

    private static BankTransaction Line(long cents, string? name = "Anna Huber", string reference = "Beitrag", string? iban = null)
        => new(ClubId, 1, 1, new BankStatementLine(new DateOnly(2026, 5, 2), null, cents, name, iban, reference, null), "f#0");

    [Fact]
    public void Fingerprint_IsTheSameForTheSameLine_AndCountsEqualLines()
    {
        var line = new BankStatementLine(new DateOnly(2026, 5, 2), null, 120, "Anna Huber", null, "Beitrag  2026", null);
        var again = line with { Reference = "beitrag 2026", ValueDate = new DateOnly(2026, 5, 3) };

        BankTransaction.FingerprintOf(line, 0).Should().Be(BankTransaction.FingerprintOf(again, 0));
        BankTransaction.FingerprintOf(line, 0).Should().NotBe(BankTransaction.FingerprintOf(line, 1));
        BankTransaction.FingerprintOf(line, 0).Should().NotBe(BankTransaction.FingerprintOf(line with { AmountCents = 121 }, 0));
    }

    [Fact]
    public void Rule_MatchesTextOrIban_InTheDirectionAndAccount()
    {
        var rule = new BankRule(ClubId, new BankRule.RuleData("ötv", null, BankRuleDirection.Outgoing, 5, null, null, null));

        rule.Matches(Line(-3_000, "ÖTV Wien")).Should().BeTrue();
        rule.Matches(Line(-3_000, "Huber", "Lizenz ÖTV")).Should().BeTrue();
        rule.Matches(Line(3_000, "ÖTV Wien")).Should().BeFalse("money came in");

        var byIban = new BankRule(ClubId, new BankRule.RuleData(null, "AT48 3200 0000 1234 5864", BankRuleDirection.Any, 5, null, null, 2));
        byIban.Matches(Line(100, iban: "AT483200000012345864")).Should().BeFalse("another money account");

        FluentActions.Invoking(() => new BankRule(ClubId, new BankRule.RuleData(" ", null, BankRuleDirection.Any, 5, null, null, null)))
            .Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingBankRuleInvalid));
    }

    [Fact]
    public void Line_IsIgnoredAndReopened()
    {
        var line = Line(100);
        line.Ignore();
        line.Status.Should().Be(BankTransactionStatus.Ignored);
        FluentActions.Invoking(line.Ignore).Should().Throw<PreconditionException>();
        line.Reopen();
        line.IsOpen.Should().BeTrue();
    }
}

public class AccountingAutomationTests
{
    [Fact]
    public void SourceKey_IsStable()
    {
        AccountingProposal.Key("clubhouse", new DateOnly(2026, 5, 1), "cash").Should().Be("clubhouse:2026-05-01:cash");
        AccountingProposal.Key("membership-order", 12, "refund").Should().Be("membership-order:12:refund");
    }

    [Fact]
    public void Source_ThatIsOn_NeedsACategory()
    {
        var setting = new AccountingSourceSetting(7, AccountingSource.MembershipFees);

        FluentActions.Invoking(() => setting.Change(AccountingAutomationMode.Book, new DateOnly(2026, 1, 1), null))
            .Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingAutomationIncomplete));
        setting.Change(AccountingAutomationMode.Off, new DateOnly(2026, 1, 1), null);
        setting.IsOn.Should().BeFalse();
        AccountingSourceSetting.CategoryKindOf(AccountingSource.Stripe).Should().Be(AccountingCategoryKind.Expense);
    }

    [Fact]
    public void Proposal_IsBookedOrDismissedOnce()
    {
        var proposal = new AccountingProposal(7, AccountingSource.MembershipFees, "membership-order:1",
            new AccountingProposal.ProposalData(new DateOnly(2026, 5, 1), "Beitrag", AccountingEntryKind.Booking, 1, null, 3, 12_000, null, "Anna"), DateTimeOffset.UnixEpoch);

        var data = proposal.ToEntryData();
        data.Lines.Should().ContainSingle().Which.Should().Be(new AccountingEntry.LineData(3, null, 12_000));
        data.CounterpartyName.Should().Be("Anna");

        proposal.Dismiss();
        FluentActions.Invoking(proposal.Dismiss).Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(AccountingErrorCode.AccountingProposalNotOpen));
    }
}
