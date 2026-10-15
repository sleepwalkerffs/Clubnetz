using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

public record EditBooking(
    [Required] int BookingEntryId,
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    int? ClientTimeZoneOffset,
    int UserId
) : ICommand
{
    public enum ErrorCode
    {
        CannotEditBookingFromThePast = 0,

        /// <summary>Kept for the numbering; the check is in <see cref="BookingContextBuilder"/> and reports <see cref="BookCourt.ErrorCode.UserNotAllowedToBook"/> (same name).</summary>
        UserNotAllowedToBook = 1
    }

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : AsyncRequestHandler<EditBooking>
    {
        protected override async Task Handle(EditBooking request, CancellationToken cancellationToken)
        {
            var existingBooking = await context.Bookings
                .Include(b => b.Players)
                .SingleRequiredAsync(b => b.Id == request.BookingEntryId, cancellationToken);

            var hasElevatedRole = await BookingEditRules.EnsureMayEdit(context, existingBooking, request.UserId, cancellationToken);

            var builder = await BookingContextBuilder.Load(
                context,
                request.PlayModeId,
                [request.CourtId],
                request.Players,
                request.UserId,
                [request.Interval],
                cancellationToken,
                editedBookingId: existingBooking.Id);

            var bookingContext = await builder.BuildContext(request.CourtId, request.Interval, request.ClientTimeZoneOffset, [], cancellationToken)
                with { SkipPastBookingValidation = hasElevatedRole };

            bookingService.EditBooking(bookingContext, existingBooking, request.Interval, request.TimeZoneInfoId, request.Comment);

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
