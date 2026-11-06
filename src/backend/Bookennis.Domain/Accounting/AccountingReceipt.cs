using System.Globalization;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.Accounting;

/// <summary>
/// A receipt (Beleg): a photo or PDF of an invoice or a till receipt. Uploaded first (it waits in the inbox without an entry),
/// then attached to the entry it proves. Filed under the number of the entry.
/// </summary>
public class AccountingReceipt : TenantDomainEntity, IAggregateRoot
{
    public const int MaxSizeInBytes = 10 * 1024 * 1024;
    public const int MaxFileNameLength = 150;
    public const int MaxPerEntry = 10;

    /// <summary>The file types of a receipt, with the content type they are served as. The content type sent by the browser is never trusted.</summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedFileTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

#pragma warning disable CS8618
    private AccountingReceipt() { }
#pragma warning restore CS8618

    public AccountingReceipt(int clubId, string? fileName, byte[] data, int? uploadedByMemberId, DateTimeOffset now)
    {
        if (data.Length > MaxSizeInBytes)
            throw new PreconditionException(AccountingErrorCode.AccountingReceiptTooLarge, [(MaxSizeInBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture)], "The file is too large.");

        var name = Path.GetFileName(fileName?.Trim() ?? "");
        var extension = Path.GetExtension(name);
        if (data.Length == 0 || !AllowedFileTypes.TryGetValue(extension, out var contentType))
            throw new PreconditionException(AccountingErrorCode.AccountingReceiptTypeNotAllowed, [string.Join(", ", AllowedFileTypes.Keys)], "The file type is not allowed.");

        if (name.Length > MaxFileNameLength)
            name = name[..(MaxFileNameLength - extension.Length)] + extension;

        ClubId = clubId;
        FileName = name;
        ContentType = contentType;
        Size = data.Length;
        UploadedAt = now;
        UploadedByMemberId = uploadedByMemberId;
        Content = new AccountingReceiptContent(data);
    }

    /// <summary>The entry the receipt proves; <c>null</c> while it waits in the inbox.</summary>
    public int? EntryId { get; private set; }

    public string FileName { get; private set; }
    public string ContentType { get; private set; }

    /// <summary>Size of the file in bytes.</summary>
    public int Size { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary><c>null</c> in support mode and once the member left the club.</summary>
    public int? UploadedByMemberId { get; private set; }

    /// <summary>Only loaded when it is included explicitly.</summary>
    public AccountingReceiptContent Content { get; private set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>Attaches the receipt to an entry. A receipt belongs to one entry only.</summary>
    public void AttachTo(AccountingEntry entry)
    {
        if (EntryId is { } current && current != entry.Id)
            throw new PreconditionException(AccountingErrorCode.AccountingReceiptAlreadyBooked, "The receipt belongs to another entry.");
        EntryId = entry.Id;
    }

    /// <summary>Back into the inbox. The caller checks that the year of the entry is open.</summary>
    public void Detach() => EntryId = null;
}

/// <summary>The file of an <see cref="AccountingReceipt"/>, in its own table so lists of receipts don't load the files.</summary>
public class AccountingReceiptContent
{
    private AccountingReceiptContent() { }

    internal AccountingReceiptContent(byte[] data) => Data = data;

    public int AccountingReceiptId { get; private set; }
    public byte[] Data { get; private set; } = [];
}
