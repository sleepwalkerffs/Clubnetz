using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>What a bank statement file says, in any format.</summary>
public record BankStatement(string? Iban, long? OpeningBalanceCents, long? ClosingBalanceCents, DateOnly? ClosingDate, IReadOnlyList<BankStatementLine> Lines);

/// <summary>A line of a statement. <c>AmountCents</c> is positive when money came in.</summary>
public record BankStatementLine(DateOnly BookingDate, DateOnly? ValueDate, long AmountCents, string? CounterpartyName, string? CounterpartyIban, string? Reference, string? BankReference);

/// <summary>
/// How a CSV export is read: the rows before the data (header lines), the separator and which column holds what (0-based).
/// The amount is in <see cref="Amount"/> (with sign, or with <see cref="DebitCreditMarker"/>) or split into <see cref="Debit"/>/<see cref="Credit"/>.
/// </summary>
public record CsvColumns
{
    public int SkipRows { get; init; }
    public char Delimiter { get; init; } = ';';
    public int BookingDate { get; init; }
    public int? ValueDate { get; init; }
    public int? Amount { get; init; }
    public int? Debit { get; init; }
    public int? Credit { get; init; }

    /// <summary>A column with S/H, D/C, Soll/Haben, Debit/Credit: the amount is negative for S/D.</summary>
    public int? DebitCreditMarker { get; init; }

    public int? CounterpartyName { get; init; }
    public int? CounterpartyIban { get; init; }

    /// <summary>The columns that are joined into the reference (purpose, payment reference, booking text).</summary>
    public IReadOnlyList<int> Reference { get; init; } = [];

    public bool IsValid => BookingDate >= 0 && (Amount is not null || Debit is not null || Credit is not null);
}

/// <summary>What reading a file gave: a statement, or (for a CSV whose columns are not known) the first rows to map them.</summary>
public record BankStatementReadResult(BankStatementFormat Format, BankStatement? Statement, IReadOnlyList<IReadOnlyList<string>>? Preview, CsvColumns? DetectedColumns)
{
    public bool NeedsCsvLayout => Statement is null;
}

/// <summary>Reads camt.053 (XML), MT940 and CSV exports of online banking into <see cref="BankStatement"/>s.</summary>
public static class BankStatementReader
{
    public const int MaxFileSize = 5 * 1024 * 1024;
    public const int PreviewRows = 12;

    /// <summary>
    /// Reads <paramref name="data"/>. A CSV is read with the columns it recognizes, else with <paramref name="savedLayout"/> (the layout
    /// mapped for this money account before); when neither works the result asks for a layout.
    /// </summary>
    /// <param name="data">The file.</param>
    /// <param name="savedLayout">The CSV columns mapped for the money account before (or just now).</param>
    /// <param name="preferSavedLayout">The layout was just mapped by the treasurer: it wins over the recognized columns.</param>
    /// <param name="accountIban">
    /// The IBAN of the money account: from a file with statements of several accounts only its statements are read. Without it, such a
    /// file is refused (it can't be told which lines belong to the account).
    /// </param>
    public static BankStatementReadResult Read(byte[] data, CsvColumns? savedLayout, bool preferSavedLayout = false, string? accountIban = null)
    {
        if (data.Length == 0 || data.Length > MaxFileSize)
            throw new PreconditionException(AccountingErrorCode.AccountingBankFileInvalid, "Empty or too large file.");

        var text = Decode(data);
        var start = text.TrimStart('﻿', ' ', '\r', '\n', '\t');
        if (start.StartsWith('<'))
            return new BankStatementReadResult(BankStatementFormat.Camt053, ReadCamt(start, Compact(accountIban)), null, null);
        if (Regex.IsMatch(text, @"(^|\n):20:", RegexOptions.None, TimeSpan.FromSeconds(1)) && text.Contains(":61:", StringComparison.Ordinal))
            return new BankStatementReadResult(BankStatementFormat.Mt940, ReadMt940(text, Compact(accountIban)), null, null);

        var delimiter = Csv.GuessDelimiter(text);
        var rows = Csv.Parse(text, delimiter);
        var detected = Csv.Detect(rows) is { } found ? found with { Delimiter = delimiter } : null;
        BankStatement? statement;
        if (preferSavedLayout && savedLayout is { IsValid: true } && Csv.TryRead(Csv.Parse(text, savedLayout.Delimiter), savedLayout, out statement))
            return new BankStatementReadResult(BankStatementFormat.Csv, statement, null, savedLayout);
        if (detected is { IsValid: true } && Csv.TryRead(rows, detected, out statement))
            return new BankStatementReadResult(BankStatementFormat.Csv, statement, null, detected);
        if (!preferSavedLayout && savedLayout is { IsValid: true } && Csv.TryRead(Csv.Parse(text, savedLayout.Delimiter), savedLayout, out statement))
            return new BankStatementReadResult(BankStatementFormat.Csv, statement, null, savedLayout);

        var preview = rows.Take(PreviewRows).ToList();
        return new BankStatementReadResult(BankStatementFormat.Csv, null, preview, detected ?? savedLayout ?? new CsvColumns { Delimiter = delimiter });
    }

    /// <summary>UTF-8 (with or without BOM); Austrian banks still export Windows-1252, read as Latin-1 then.</summary>
    private static string Decode(byte[] data)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(data).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(data);
        }
    }

    // ---------- camt.053 ----------

    private static string? Compact(string? iban) => iban?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    /// <summary>Whether a statement of <paramref name="statementIban"/> belongs to the account (statements without IBAN are taken).</summary>
    private static bool OfAccount(string? statementIban, string? accountIban)
        => accountIban is null || statementIban is null || string.Equals(Compact(statementIban), accountIban, StringComparison.Ordinal);

    private static void EnsureOneAccount(IEnumerable<string?> ibans, string? accountIban)
    {
        if (accountIban is null && ibans.Where(i => i is not null).Select(Compact).Distinct().Count() > 1)
            throw new PreconditionException(AccountingErrorCode.AccountingBankFileSeveralAccounts, "The file holds statements of several accounts.");
    }

    private static BankStatement ReadCamt(string xml, string? accountIban)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new PreconditionException(AccountingErrorCode.AccountingBankFileInvalid, $"Invalid XML: {exception.Message}");
        }

        // camt.053 (Stmt), also camt.052 (Rpt) and camt.054 (Ntfctn): the entries look the same
        var statements = document.Descendants().Where(e => e.Name.LocalName is "Stmt" or "Rpt" or "Ntfctn").ToList();
        if (statements.Count == 0)
            throw new PreconditionException(AccountingErrorCode.AccountingBankFileInvalid, "No statement in the XML.");

        var lines = new List<BankStatementLine>();
        var ibans = statements.Select(s => Path(s, "Acct", "Id", "IBAN")?.Value).ToList();
        EnsureOneAccount(ibans, accountIban);
        string? iban = ibans.FirstOrDefault(i => i is not null && OfAccount(i, accountIban)) ?? ibans.FirstOrDefault(i => i is not null);
        long? opening = null, closing = null;
        DateOnly? closingDate = null, openingDate = null;
        foreach (var statement in statements)
        {
            if (!OfAccount(Path(statement, "Acct", "Id", "IBAN")?.Value, accountIban))
                continue; // another account of the same customer
            foreach (var balance in Children(statement, "Bal"))
            {
                var type = Path(balance, "Tp", "CdOrPrtry", "Cd")?.Value;
                var amount = SignedCents(balance);
                var date = Date(Path(balance, "Dt", "Dt") ?? Path(balance, "Dt", "DtTm"));
                if (type is "OPBD" or "PRCD" && amount is not null && (openingDate is null || date < openingDate))
                {
                    opening = amount;
                    openingDate = date;
                }
                if (type is "CLBD" && amount is not null && (closingDate is null || date > closingDate))
                {
                    closing = amount;
                    closingDate = date;
                }
            }

            foreach (var entry in Children(statement, "Ntry"))
            {
                var status = Path(entry, "Sts", "Cd")?.Value ?? Path(entry, "Sts")?.Value;
                if (status is not null && status.Trim() is not ("BOOK" or ""))
                    continue; // pending or info lines are not booked yet

                var bookingDate = Date(Path(entry, "BookgDt", "Dt") ?? Path(entry, "BookgDt", "DtTm"))
                                  ?? Date(Path(entry, "ValDt", "Dt") ?? Path(entry, "ValDt", "DtTm"));
                if (bookingDate is null)
                    continue;
                var valueDate = Date(Path(entry, "ValDt", "Dt") ?? Path(entry, "ValDt", "DtTm"));
                var entryAmount = SignedCents(entry) ?? 0;
                var credit = IsCredit(entry);
                var bankReference = Path(entry, "AcctSvcrRef")?.Value;
                var additional = Child(entry, "AddtlNtryInf")?.Value;

                var details = entry.Elements().Where(e => e.Name.LocalName == "NtryDtls").SelectMany(d => Children(d, "TxDtls")).ToList();
                var split = details.Count > 1 && details.All(d => Path(d, "AmtDtls", "TxAmt", "Amt") is not null || Child(d, "Amt") is not null);
                if (!split)
                {
                    var detail = details.FirstOrDefault();
                    lines.Add(Line(detail, bookingDate.Value, valueDate, entryAmount, credit, bankReference, additional));
                    continue;
                }

                // A batch (Sammelbuchung): one line per transaction
                foreach (var detail in details)
                {
                    var amountElement = Path(detail, "AmtDtls", "TxAmt", "Amt") ?? Child(detail, "Amt")!;
                    var cents = Cents(amountElement.Value);
                    var detailCredit = Child(detail, "CdtDbtInd") is { } indicator ? indicator.Value == "CRDT" : credit;
                    lines.Add(Line(detail, bookingDate.Value, valueDate, detailCredit ? cents : -cents, detailCredit, bankReference, additional));
                }
            }
        }

        return new BankStatement(iban, opening, closing, closingDate, lines.Where(l => l.AmountCents != 0).ToList());
    }

    private static BankStatementLine Line(XElement? detail, DateOnly bookingDate, DateOnly? valueDate, long amount, bool credit, string? bankReference, string? additional)
    {
        // The other party: who paid us (debtor) for money that came in, who we paid (creditor) for money that went out
        var party = credit ? "Dbtr" : "Cdtr";
        var parties = detail is null ? null : Child(detail, "RltdPties");
        var name = parties is null ? null : (Path(parties, party, "Nm") ?? Path(parties, party, "Pty", "Nm"))?.Value;
        var iban = parties is null ? null : Path(parties, party + "Acct", "Id", "IBAN")?.Value;

        var remittance = detail is null ? null : Child(detail, "RmtInf");
        var texts = new List<string>();
        if (remittance is not null)
        {
            texts.AddRange(Children(remittance, "Ustrd").Select(u => u.Value.Trim()));
            texts.AddRange(remittance.Descendants().Where(e => e.Name.LocalName == "Ref" && e.Parent?.Name.LocalName == "CdtrRefInf").Select(e => e.Value.Trim()));
        }
        if (detail is not null && Child(detail, "AddtlTxInf") is { } extra)
            texts.Add(extra.Value.Trim());
        if (texts.Count == 0 && additional is not null)
            texts.Add(additional.Trim());

        var endToEnd = detail is null ? null : Path(detail, "Refs", "EndToEndId")?.Value;
        if (endToEnd == "NOTPROVIDED")
            endToEnd = null;

        return new BankStatementLine(bookingDate, valueDate, amount, name, iban, string.Join(" ", texts.Where(t => t.Length > 0)), endToEnd ?? bankReference);
    }

    private static long? SignedCents(XElement element)
    {
        var amount = Child(element, "Amt");
        if (amount is null)
            return null;
        var cents = Cents(amount.Value);
        return IsCredit(element) ? cents : -cents;
    }

    private static bool IsCredit(XElement element) => Child(element, "CdtDbtInd")?.Value != "DBIT";

    private static long Cents(string value)
        => decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)
            ? (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)
            : throw new PreconditionException(AccountingErrorCode.AccountingBankFileInvalid, $"Invalid amount '{value}'.");

    private static DateOnly? Date(XElement? element)
    {
        if (element is null)
            return null;
        var text = element.Value.Trim();
        return DateOnly.TryParseExact(text.Length >= 10 ? text[..10] : text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private static XElement? Child(XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static IEnumerable<XElement> Children(XElement element, string name) => element.Elements().Where(e => e.Name.LocalName == name);

    private static XElement? Path(XElement element, params string[] names)
    {
        XElement? current = element;
        foreach (var name in names)
            current = current is null ? null : Child(current, name);
        return current;
    }

    // ---------- MT940 ----------

    private static BankStatement ReadMt940(string text, string? accountIban)
    {
        var fields = Mt940Fields(text);
        var ibans = fields.Where(f => f.Tag == "25").Select(f => Mt940Iban(f.Value)).ToList();
        EnsureOneAccount(ibans, accountIban);

        var lines = new List<BankStatementLine>();
        string? iban = ibans.FirstOrDefault(i => i is not null && OfAccount(i, accountIban)) ?? ibans.FirstOrDefault(i => i is not null);
        long? opening = null, closing = null;
        DateOnly? closingDate = null;
        var openingTaken = false;
        var included = true;

        for (var i = 0; i < fields.Count; i++)
        {
            var (tag, value) = fields[i];
            if (tag == "25")
            {
                // A new statement: only the ones of the account count (a file can hold several accounts of the same customer)
                included = OfAccount(Mt940Iban(value), accountIban);
                continue;
            }
            if (!included)
                continue;

            switch (tag)
            {
                case "60F" or "60M" when !openingTaken:
                    (opening, _) = Mt940Balance(value);
                    openingTaken = true;
                    break;
                case "62F" or "62M":
                    (closing, closingDate) = Mt940Balance(value);
                    break;
                case "61":
                    var info = i + 1 < fields.Count && fields[i + 1].Tag == "86" ? fields[i + 1].Value : null;
                    if (Mt940Line(value, info) is { } line)
                        lines.Add(line);
                    break;
            }
        }

        // A line of 0.00 (fee information of some banks) is no movement
        return new BankStatement(iban, opening, closing, closingDate, lines.Where(l => l.AmountCents != 0).ToList());
    }

    private static string? Mt940Iban(string account)
        => Regex.Match(account.Trim(), @"[A-Z]{2}\d{2}[A-Z0-9]{10,30}", RegexOptions.None, TimeSpan.FromSeconds(1)) is { Success: true } m ? m.Value : null;

    private static List<(string Tag, string Value)> Mt940Fields(string text)
    {
        var fields = new List<(string Tag, string Value)>();
        foreach (var raw in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            var match = Regex.Match(line, @"^:(\d{2}[A-Z]?):(.*)$", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (match.Success)
                fields.Add((match.Groups[1].Value, match.Groups[2].Value));
            else if (fields.Count > 0 && line.Length > 0 && line != "-" && !line.StartsWith('{'))
                fields[^1] = (fields[^1].Tag, fields[^1].Value + "\n" + line);
        }
        return fields;
    }

    private static (long? Cents, DateOnly? Date) Mt940Balance(string value)
    {
        var match = Regex.Match(value.Trim(), @"^([CD])(\d{6})[A-Z]{3}([\d,]+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!match.Success)
            return (null, null);
        var cents = Mt940Cents(match.Groups[3].Value);
        return (match.Groups[1].Value == "D" ? -cents : cents, Mt940Date(match.Groups[2].Value));
    }

    private static BankStatementLine? Mt940Line(string value, string? info)
    {
        // :61:YYMMDD[MMDD](C|D|RC|RD)[funds code]amount N|F|S... //bank reference
        var match = Regex.Match(value.Replace("\n", "", StringComparison.Ordinal),
            @"^(\d{6})(\d{4})?(RC|RD|C|D)[A-Z]?([\d,]+)([NFS][A-Z0-9]{3})?([^/]*)(//(\S+))?", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!match.Success)
            return null;

        var valueDate = Mt940Date(match.Groups[1].Value);
        if (valueDate is null)
            return null;
        var bookingDate = valueDate.Value;
        if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value[..2], CultureInfo.InvariantCulture, out var month) && int.TryParse(match.Groups[2].Value[2..], CultureInfo.InvariantCulture, out var day)
            && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(valueDate.Value.Year, month))
        {
            bookingDate = new DateOnly(valueDate.Value.Year, month, day);
            // A booking in early January for a value date in late December belongs to the next year (and the other way round)
            if (bookingDate.DayNumber - valueDate.Value.DayNumber > 180)
                bookingDate = bookingDate.AddYears(-1);
            else if (valueDate.Value.DayNumber - bookingDate.DayNumber > 180)
                bookingDate = bookingDate.AddYears(1);
        }

        var mark = match.Groups[3].Value;
        var cents = Mt940Cents(match.Groups[4].Value);
        var credit = mark is "C" or "RD"; // a reversal of a debit brings money back
        var customerReference = match.Groups[6].Value.Trim();
        var bankReference = match.Groups[8].Success ? match.Groups[8].Value : null;

        var (name, iban, reference) = Mt940Info(info);
        if (string.IsNullOrWhiteSpace(reference) && customerReference is not ("" or "NONREF"))
            reference = customerReference;

        return new BankStatementLine(bookingDate, valueDate, credit ? cents : -cents, name, iban, reference, bankReference);
    }

    /// <summary>The :86: field: structured with ?20..?63 subfields (German/Austrian banks), else free text.</summary>
    private static (string? Name, string? Iban, string? Reference) Mt940Info(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return (null, null, null);

        var flat = info.Replace("\n", "", StringComparison.Ordinal);
        if (!Regex.IsMatch(flat, @"\?\d{2}", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            var text = info.Replace("\n", " ", StringComparison.Ordinal).Trim();
            var ibanMatch = Regex.Match(text, @"\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b", RegexOptions.None, TimeSpan.FromSeconds(1));
            return (null, ibanMatch.Success ? ibanMatch.Value : null, text);
        }

        var parts = Regex.Matches(flat, @"\?(\d{2})([^?]*)", RegexOptions.None, TimeSpan.FromSeconds(1))
            .Select(m => (Code: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Text: m.Groups[2].Value))
            .ToList();
        var reference = string.Concat(parts.Where(p => p.Code is >= 20 and <= 29 or >= 60 and <= 63).Select(p => p.Text)).Trim();
        reference = Regex.Replace(reference, @"^(SVWZ\+|EREF\+\S+\s*)", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        var name = string.Concat(parts.Where(p => p.Code is 32 or 33).Select(p => p.Text)).Trim();
        var account = parts.FirstOrDefault(p => p.Code == 31).Text?.Trim();
        var iban = account is not null && Regex.IsMatch(account, @"^[A-Z]{2}\d{2}", RegexOptions.None, TimeSpan.FromSeconds(1)) ? account : null;
        return (name.Length > 0 ? name : null, iban, reference);
    }

    private static long Mt940Cents(string value)
        => decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)
            : throw new PreconditionException(AccountingErrorCode.AccountingBankFileInvalid, $"Invalid amount '{value}'.");

    private static DateOnly? Mt940Date(string value)
        => DateOnly.TryParseExact(value, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    // ---------- CSV ----------

    /// <summary>Splitting, recognizing and reading CSV exports.</summary>
    public static class Csv
    {
        private static readonly string[] DateFormats =
            ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyyMMdd", "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"];

        // Header names of the columns, as Austrian and German online banking exports them (compared without case, spaces and punctuation)
        private static readonly string[] BookingDateHeaders = ["buchungsdatum", "buchungstag", "datum", "bookingdate", "date", "transactiondate", "umsatzdatum", "belegdatum"];
        private static readonly string[] ValueDateHeaders = ["valutadatum", "valuta", "wertstellung", "valuedate", "wertstellungsdatum"];
        private static readonly string[] AmountHeaders = ["betrag", "umsatz", "amount", "betrageur", "umsatzeur", "buchungsbetrag", "betragineur"];
        private static readonly string[] DebitHeaders = ["soll", "debit", "ausgang", "lastschrift", "belastung"];
        private static readonly string[] CreditHeaders = ["haben", "credit", "eingang", "gutschrift"];
        private static readonly string[] MarkerHeaders = ["sollhaben", "sh", "kennzeichen", "debitcredit", "dc"];
        private static readonly string[] NameHeaders =
        [
            "namedespartners", "partnername", "empfängerauftraggeber", "auftraggeberempfänger", "auftraggeberzahlungsempfänger", "zahlungsempfängerauftraggeber",
            "namezahlungsbeteiligter", "begünstigterzahlungspflichtiger", "empfänger", "auftraggeber", "zahlungspartner", "name", "partner",
            "gegenkontoname", "beguenstigterzahlungspflichtiger", "empfaengerauftraggeber", "counterparty", "payee", "nameauftraggeberempfänger",
        ];
        // A plain "IBAN" comes last: in the Volksbank export it is the own account, the partner's is "Kontoverbindung des Partners"
        private static readonly string[] IbanHeaders =
        [
            "kontoverbindungdespartners", "partneriban", "ibanzahlungsbeteiligter", "gegenkontoiban", "ibanauftraggeberempfänger", "counterpartyiban",
            "kontonummeriban", "gegenkonto", "iban",
        ];
        private static readonly string[] ReferenceHeaders =
        [
            "verwendungszweck", "zahlungsreferenz", "buchungstext", "umsatztext", "buchungsinfo", "text", "reference", "description", "beschreibung",
            "mitteilung", "zweck", "details", "buchungsdetails", "umsatzart", "info",
        ];

        public static char GuessDelimiter(string text)
        {
            var sample = string.Join('\n', text.Split('\n').Take(30));
            return new[] { ';', ',', '\t', '|' }.MaxBy(d => sample.Count(c => c == d));
        }

        public static List<IReadOnlyList<string>> Parse(string text, char? delimiter = null)
        {
            var separator = delimiter ?? GuessDelimiter(text);
            var rows = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                        quoted = false;
                    else
                        cell.Append(c);
                }
                else if (c == '"' && cell.Length == 0)
                    quoted = true;
                else if (c == separator)
                {
                    row.Add(cell.ToString().Trim());
                    cell.Clear();
                }
                else if (c is '\n' or '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(cell.ToString().Trim());
                    cell.Clear();
                    if (row.Any(v => v.Length > 0))
                        rows.Add(row);
                    row = [];
                }
                else
                    cell.Append(c);
            }

            row.Add(cell.ToString().Trim());
            if (row.Any(v => v.Length > 0))
                rows.Add(row);
            return rows;
        }

        /// <summary>Finds the header row and the columns by their names; or a file without header (Raiffeisen ELBA: date;text;value date;amount;currency).</summary>
        public static CsvColumns? Detect(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            for (var r = 0; r < Math.Min(rows.Count, 30); r++)
            {
                var headers = rows[r].Select(Normalize).ToList();
                var date = Find(headers, BookingDateHeaders);
                var amount = Find(headers, AmountHeaders);
                var debit = Find(headers, DebitHeaders);
                var credit = Find(headers, CreditHeaders);
                if (date is null || amount is null && debit is null && credit is null)
                    continue;

                var references = headers.Select((h, i) => (h, i))
                    .Where(x => ReferenceHeaders.Contains(x.h) && x.i != date && x.i != amount)
                    .Select(x => x.i)
                    .ToList();
                return new CsvColumns
                {
                    SkipRows = r + 1,
                    BookingDate = date.Value,
                    ValueDate = Find(headers, ValueDateHeaders),
                    Amount = amount,
                    Debit = amount is null ? debit : null,
                    Credit = amount is null ? credit : null,
                    DebitCreditMarker = Find(headers, MarkerHeaders),
                    CounterpartyName = Find(headers, NameHeaders),
                    CounterpartyIban = Find(headers, IbanHeaders),
                    Reference = references,
                };
            }

            // Without header: date; text; date; amount; currency (Raiffeisen ELBA)
            if (rows.Count > 0 && rows.All(row => row.Count >= 4 && ParseDate(row[0]) is not null && ParseDate(row[2]) is not null && ParseAmount(row[3]) is not null))
                return new CsvColumns { BookingDate = 0, ValueDate = 2, Amount = 3, Reference = [1] };

            return null;
        }

        /// <summary>Reads every row after <see cref="CsvColumns.SkipRows"/>; <c>false</c> when the columns don't fit (no row could be read).</summary>
        public static bool TryRead(IReadOnlyList<IReadOnlyList<string>> rows, CsvColumns columns, out BankStatement? statement)
        {
            statement = null;
            var lines = new List<BankStatementLine>();
            var failed = 0;
            foreach (var row in rows.Skip(columns.SkipRows))
            {
                if (Line(row, columns) is { } line)
                    lines.Add(line);
                else
                    failed++;
            }

            // A few rows that are no transactions are fine (totals, balances at the end), most rows must be
            if (lines.Count == 0 || failed > Math.Max(3, lines.Count / 2))
                return false;

            statement = new BankStatement(null, null, null, null, lines.Where(l => l.AmountCents != 0).ToList());
            return true;
        }

        private static BankStatementLine? Line(IReadOnlyList<string> row, CsvColumns columns)
        {
            string? Cell(int? index) => index is { } i && i >= 0 && i < row.Count && row[i].Length > 0 ? row[i] : null;

            var date = ParseDate(Cell(columns.BookingDate));
            if (date is null)
                return null;

            long? amount;
            if (columns.Amount is not null)
                amount = ParseAmount(Cell(columns.Amount));
            else
            {
                var debit = ParseAmount(Cell(columns.Debit));
                var credit = ParseAmount(Cell(columns.Credit));
                amount = debit is null && credit is null ? null : (credit ?? 0) - Math.Abs(debit ?? 0);
            }
            if (amount is null)
                return null;

            if (Cell(columns.DebitCreditMarker) is { } marker && Normalize(marker) is "s" or "d" or "soll" or "debit" or "dbit" or "belastung")
                amount = -Math.Abs(amount.Value);

            // A text another column already holds is left out: the Volksbank "Umsatztext" repeats the "Verwendungszweck"
            var texts = columns.Reference.Select(i => Cell(i)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var reference = string.Join(" ", texts.Where(t => !texts.Any(o => o.Length > t.Length && o.Contains(t, StringComparison.OrdinalIgnoreCase))));
            return new BankStatementLine(date.Value, ParseDate(Cell(columns.ValueDate)), amount.Value, Cell(columns.CounterpartyName), Cell(columns.CounterpartyIban), reference, null);
        }

        public static DateOnly? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var text = value.Trim();
            return DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? DateOnly.FromDateTime(date) : null;
        }

        /// <summary>"-1.234,56", "1,234.56", "1234,56 S", "+12,00 EUR", "12,00-": the amount in cents (S/- = negative).</summary>
        public static long? ParseAmount(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var text = value.Trim().Replace("EUR", "", StringComparison.OrdinalIgnoreCase).Replace("€", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
            var negative = false;
            if (text.EndsWith('S') || text.EndsWith('-'))
            {
                negative = true;
                text = text[..^1];
            }
            else if (text.EndsWith('H') || text.EndsWith('+'))
                text = text[..^1];
            if (text.StartsWith('-'))
            {
                negative = !negative;
                text = text[1..];
            }
            else if (text.StartsWith('+'))
                text = text[1..];

            if (text.Length == 0 || !text.All(c => char.IsDigit(c) || c is '.' or ',' or '\''))
                return null;

            // The last separator followed by 1-2 digits is the decimal one; the others group thousands
            text = text.Replace("'", "", StringComparison.Ordinal);
            var last = text.LastIndexOfAny(['.', ',']);
            string whole, fraction;
            if (last >= 0 && text.Length - last - 1 is 1 or 2)
            {
                whole = text[..last].Replace(".", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
                fraction = text[(last + 1)..].PadRight(2, '0');
            }
            else
            {
                whole = text.Replace(".", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
                fraction = "00";
            }

            if (whole.Length == 0)
                whole = "0";
            if (whole.Length > 12 || !long.TryParse(whole + fraction, NumberStyles.None, CultureInfo.InvariantCulture, out var cents))
                return null;
            return negative ? -cents : cents;
        }

        private static int? Find(List<string> headers, string[] names)
        {
            foreach (var name in names)
            {
                var index = headers.IndexOf(name);
                if (index >= 0)
                    return index;
            }
            return null;
        }

        private static string Normalize(string value)
            => string.Concat(value.ToLowerInvariant().Where(char.IsLetterOrDigit));
    }
}
