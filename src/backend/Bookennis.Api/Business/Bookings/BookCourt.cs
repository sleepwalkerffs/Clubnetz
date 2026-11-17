using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;

namespace Bookennis.Api.Business.Bookings;

/// <remarks><c>TimeZoneInfoId</c>: Ignored: bookings are stored with the club's time zone (<c>Club.TimeZoneId</c>).</remarks>
public record BookCourt(
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    string? TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    int UserId
) : ICommand<BookCourtResult>
{
    public enum ErrorCode
    {
        UserNotAllowedToBook = 0
    }

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : IRequestHandler<BookCourt, BookCourtResult>
    {
        public async Task<BookCourtResult> Handle(BookCourt request, CancellationToken cancellationToken)
        {
            var builder = await BookingContextBuilder.Load(
                context,
                request.PlayModeId,
                [request.CourtId],
                request.Players,
                request.UserId,
                [request.Interval],
                cancellationToken);

            var bookingContext = await builder.BuildContext(request.CourtId, request.Interval, [], cancellationToken);

            var booking = context.Add(bookingService.BookCourt(bookingContext, request.Interval, builder.Region.TimeZoneId, request.Comment)).Entity;
            await context.SaveChangesAsync(cancellationToken);

            return new BookCourtResult { Id = booking.Id };
        }
    }
}
