using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Booking;

// CA1720: 'Single' contains a type name but it is the correct domain term for
// "only this one occurrence" in a recurring booking context.
#pragma warning disable CA1720
public enum RecurringDeleteScope
{
    Single,
    ThisAndFollowing
}
#pragma warning restore CA1720

public record DeleteRecurringBookingModel(
    [Required] int BookingEntryId,
    [Required] RecurringDeleteScope Scope);
