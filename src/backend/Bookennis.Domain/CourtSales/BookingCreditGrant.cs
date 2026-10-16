using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.CourtSales;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.CourtSales.BookingCreditReason</c>.</summary>
public enum BookingCreditReason
{
    /// <summary>The bookings left on the guest cards of the former guest system.</summary>
    GuestCardMigration = 0,

    /// <summary>A treasurer or admin gave (or took back) credits.</summary>
    GrantedByClub = 1,
}

/// <summary>
/// Booking credits given to a member: one credit pays for one bought booking of up to an hour.
/// The balance is the sum of the grants minus the purchases that use a credit and are not expired, failed or cancelled,
/// so a credit comes back by itself when its purchase ends that way. A negative grant corrects a balance.
/// </summary>
[Guid("8D41F7B2-3C9E-4E05-A6B8-5F2D0C7E9A13")]
public class BookingCreditGrant : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNoteLength = 500;
    public const int MaxCount = 1000;

#pragma warning disable CS8618
    private BookingCreditGrant() { }
#pragma warning restore CS8618

    public BookingCreditGrant(int clubId, int memberId, int count, BookingCreditReason reason, string? note, int? grantedByMemberId)
    {
        if (count == 0 || Math.Abs(count) > MaxCount)
            throw new PreconditionException(CourtSaleErrorCode.BookingCreditCountInvalid, [MaxCount.ToString()], "Invalid number of credits.");

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > MaxNoteLength)
            throw new PreconditionException(CourtSaleErrorCode.BookingCreditNoteTooLong, [MaxNoteLength.ToString()], "The note is too long.");

        ClubId = clubId;
        MemberId = memberId;
        Count = count;
        Reason = reason;
        Note = trimmed;
        GrantedByMemberId = grantedByMemberId;
    }

    public int MemberId { get; private set; }

    /// <summary>Never 0; negative to take credits back.</summary>
    public int Count { get; private set; }

    public BookingCreditReason Reason { get; private set; }

    /// <summary>For the treasurers.</summary>
    public string? Note { get; private set; }

    /// <summary><c>null</c> for the migration, in support mode and once the member who granted it left.</summary>
    public int? GrantedByMemberId { get; private set; }
}
