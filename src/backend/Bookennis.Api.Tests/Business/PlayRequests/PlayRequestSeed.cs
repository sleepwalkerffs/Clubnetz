using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.PlayRequests;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using NSubstitute;
using NSubstitute.ClearExtensions;

namespace Bookennis.Api.Tests.Business.PlayRequests;

internal static class PlayRequestSeed
{
    /// <summary>A day in the future, so the requests are neither in the past nor expired.</summary>
    public static DateTimeOffset Start => new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(3).Date, TimeSpan.Zero).AddHours(17);

    /// <summary>
    /// A member who can book the play mode of the test club (role User) and is active (staff role, so no season is needed).
    /// The seeded <see cref="TestDataSeed.Member2Id"/> (admin) is active too; <see cref="TestDataSeed.Member1Id"/> is not.
    /// </summary>
    public static async Task<(int UserId, int MemberId)> AddPlayer(AppDbContext context, string name)
    {
        var user = new User($"{name}@bookennis.com", $"{name}@bookennis.com", name, "Player", new DateOnly(1990, 1, 1), Gender.Female);
        context.Add(user);
        await context.SaveChangesAsync();

        var member = new ClubMember(user.Id, TestDataSeed.ClubId, [MemberRole.User, MemberRole.Trainer]);
        context.Add(member);
        await context.SaveChangesAsync();
        return (user.Id, member.Id);
    }

    public static async Task<PlayRequest> AddRequest(
        AppDbContext context,
        int requesterMemberId,
        int[] inviteeIds,
        int playersNeeded = 2,
        int[]? courtIds = null,
        DateTimeOffset? start = null,
        DateTimeOffset? expiresAt = null)
    {
        var from = start ?? Start;
        var playModeId = context.PlayModes.First(p => p.ClubId == TestDataSeed.ClubId).Id;
        var request = PlayRequest.Create(
            TestDataSeed.ClubId,
            requesterMemberId,
            new PlayRequest.RequestData(
                playModeId,
                new DateTimeOffsetInterval(from, from.AddMinutes(90)),
                TimeZoneInfo.Utc.Id,
                courtIds ?? [TestDataSeed.Court1Id, TestDataSeed.Court2Id],
                playersNeeded,
                inviteeIds,
                null,
                expiresAt),
            DateTimeOffset.UtcNow);
        context.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    /// <summary>The domain service books like the real one would when every rule is met; <paramref name="occupiedCourtIds"/> are taken.</summary>
    public static IBookingDomainService BookingServiceBooksUnlessOccupied(IBookingDomainService service, params int[] occupiedCourtIds)
    {
        service.ClearSubstitute();
        service
            .BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(call =>
            {
                var context = call.Arg<BookingDomainService.Context>();
                if (occupiedCourtIds.Contains(context.Court.Id))
                    throw new PreconditionException(BookingDomainService.ErrorCode.CourtOccupied, "Court is already occupied during booked time");

                return new Booking(context.ClubId, context.Court.Id, context.PlayMode.Id, call.ArgAt<DateTimeOffsetInterval>(1), call.ArgAt<string>(2), context.Participants.ConvertAll(p => p.Member.Id));
            });
        return service;
    }
}
