using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;

namespace Bookennis.Api.Business.Bookings;

public record BookCourt(
    [Required] int CourtId,
    [Required] DateTimeOffsetInterval Interval,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    int? ClientTimeZoneOffset,
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

            var bookingContext = await builder.BuildContext(request.CourtId, request.Interval, request.ClientTimeZoneOffset, [], cancellationToken);

            var booking = context.Add(bookingService.BookCourt(bookingContext, request.Interval, request.TimeZoneInfoId, request.Comment)).Entity;
            await context.SaveChangesAsync(cancellationToken);

            return new BookCourtResult { Id = booking.Id };
        }
    }
}
