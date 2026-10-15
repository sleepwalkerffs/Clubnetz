using System.ComponentModel.DataAnnotations;
using System.Runtime.ExceptionServices;
using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

/// <summary>
/// Books several slots (court and time) at once with the same play mode and players: the multi-day planner.
/// Every slot is validated by <see cref="IBookingDomainService.BookCourt"/> with the same context as a single booking
/// (<see cref="BookingContextBuilder"/>); the earlier slots of the batch count like existing bookings.
/// Nothing is saved unless every slot is valid. A dry run only reports the result.
/// </summary>
public record BookCourts(
    [Required] List<BookingSlotModel> Slots,
    [Required] string TimeZoneInfoId,
    [Required] int PlayModeId,
    [Required] List<int> Players,
    string? Comment,
    bool DryRun,
    int? ClientTimeZoneOffset,
    int UserId,
    List<int>? ExpectedBookingIdsToRemove = null
) : ICommand<BookCourtsResult>
{
    public const int MaxSlots = 100;

    public enum ErrorCode
    {
        /// <summary>Less than one or more than <see cref="MaxSlots"/> slots. Detail: the maximum.</summary>
        InvalidSlotCount = 0,

        /// <summary>The slot overlaps an earlier slot of the same batch on the same court. Detail: the number (1-based) of that slot.</summary>
        BookingSlotsOverlap = 1,

        /// <summary>
        /// The bookings an overbooking play mode would remove are not the ones the user confirmed (<see cref="ExpectedBookingIdsToRemove"/>):
        /// somebody booked or deleted in the meantime.
        /// </summary>
        BookingsToRemoveChanged = 2,
    }

    public class Handler(AppDbContext context, IBookingDomainService bookingService) : IRequestHandler<BookCourts, BookCourtsResult>
    {
        public async Task<BookCourtsResult> Handle(BookCourts request, CancellationToken cancellationToken)
        {
            if (request.Slots is not { Count: >= 1 and <= MaxSlots })
                throw new PreconditionException(ErrorCode.InvalidSlotCount, [MaxSlots.ToString()], $"Between 1 and {MaxSlots} slots can be booked at once");

            var slots = request.Slots.Select((slot, index) => new Slot(index, slot.CourtId, ToInterval(slot))).ToList();

            var builder = await BookingContextBuilder.Load(
                context,
                request.PlayModeId,
                slots.ConvertAll(s => s.CourtId),
                request.Players,
                request.UserId,
                slots.Where(s => s.Interval is not null).Select(s => s.Interval!).ToList(),
                cancellationToken);

            var checks = new List<BookingSlotCheck>(slots.Count);
            var bookings = new List<Booking>(slots.Count);
            var pendingIntervals = new List<DateTimeOffsetInterval>(slots.Count);
            var bookingsToRemove = new Dictionary<int, Booking>();
            PreconditionException? firstError = null;

            foreach (var slot in slots)
            {
                try
                {
                    var interval = slot.Interval ?? throw new PreconditionException(BookingDomainService.ErrorCode.InvalidTime, "Cannot play this amount of time");

                    // Even an overbooking play mode must not overlap its own slots: the second booking would delete the first one
                    var overlapping = slots.Take(slot.Index).FirstOrDefault(s => s.CourtId == slot.CourtId && s.Interval is not null && s.Interval.Intersects(interval));
                    if (overlapping is not null)
                        throw new PreconditionException(ErrorCode.BookingSlotsOverlap, [(overlapping.Index + 1).ToString()], "The slot overlaps another slot on the same court");

                    var bookingContext = await builder.BuildContext(slot.CourtId, interval, request.ClientTimeZoneOffset, pendingIntervals, cancellationToken);
                    var booking = bookingService.BookCourt(bookingContext, interval, request.TimeZoneInfoId, request.Comment);

                    bookings.Add(booking);
                    pendingIntervals.Add(interval);
                    checks.Add(new BookingSlotCheck(slot.Index, null, []));

                    // Only an overbooking play mode gets past intersecting bookings; they are deleted when the new booking is saved
                    if (builder.PlayMode.CanOverbook)
                    {
                        foreach (var intersectingBooking in bookingContext.IntersectingBookings)
                            bookingsToRemove.TryAdd(intersectingBooking.Id, intersectingBooking);
                    }
                }
                catch (PreconditionException exception)
                {
                    checks.Add(new BookingSlotCheck(slot.Index, exception.ErrorCode, exception.ErrorDetails ?? []));
                    firstError ??= exception;
                }
            }

            if (!request.DryRun && firstError is not null)
                ExceptionDispatchInfo.Throw(firstError);

            // The user confirmed which bookings are removed; if that changed since the dry run, they have to look again
            if (!request.DryRun && request.ExpectedBookingIdsToRemove is not null && !request.ExpectedBookingIdsToRemove.ToHashSet().SetEquals(bookingsToRemove.Keys))
                throw new PreconditionException(ErrorCode.BookingsToRemoveChanged, "The bookings that would be removed changed in the meantime");

            // Read before saving: saving the new bookings deletes them (DeleteIntersectingBookings)
            var removed = await LoadBookingsToRemove(builder.Club.Id, bookingsToRemove.Values.ToList(), cancellationToken);

            if (request.DryRun)
                return new BookCourtsResult(checks, removed, []);

            context.AddRange(bookings);
            await context.SaveChangesAsync(cancellationToken);

            return new BookCourtsResult(checks, removed, bookings.ConvertAll(b => b.Id));
        }

        private static DateTimeOffsetInterval? ToInterval(BookingSlotModel slot)
        {
            var to = slot.To.ToOffset(slot.From.Offset);
            return to > slot.From ? new DateTimeOffsetInterval(slot.From, to) : null;
        }

        /// <summary>The bookings an overbooking play mode removes, with the names of their players (name only, see Member Data &amp; Privacy).</summary>
        private async Task<List<BookingToRemoveDto>> LoadBookingsToRemove(int clubId, List<Booking> bookings, CancellationToken cancellationToken)
        {
            if (bookings.Count == 0)
                return [];

            var bookingIds = bookings.Select(b => b.Id).ToList();

            var playModeNames = await (
                from booking in context.Bookings
                join playMode in context.PlayModes on booking.PlayModeId equals playMode.Id
                where bookingIds.Contains(booking.Id) && booking.ClubId == clubId
                select new { booking.Id, playMode.Name }
            ).ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

            // Players of ATP clubs are members of another club: the tenant filter is off, the bookings are filtered by the club
            var players = await (
                from booking in context.Bookings.IgnoreQueryFilters()
                join bookingPlayer in context.BookingPlayers on booking.Id equals bookingPlayer.BookingEntryId
                join member in context.Set<Member>() on bookingPlayer.MemberId equals member.Id
                join user in context.Users on member.UserId equals user.Id
                where bookingIds.Contains(booking.Id) && booking.ClubId == clubId
                orderby user.LastName, user.FirstName
                select new { BookingId = booking.Id, user.FirstName, user.LastName }
            ).ToListAsync(cancellationToken);

            var playersByBooking = players.ToLookup(p => p.BookingId, p => $"{p.FirstName} {p.LastName}");

            return bookings
                .Where(b => playModeNames.ContainsKey(b.Id))
                .OrderBy(b => b.Interval.From)
                .ThenBy(b => b.CourtId)
                .Select(b => new BookingToRemoveDto(b.Id, b.CourtId, b.Interval, playModeNames[b.Id], playersByBooking[b.Id].ToList()))
                .ToList();
        }

        private sealed record Slot(int Index, int CourtId, DateTimeOffsetInterval? Interval);
    }
}
