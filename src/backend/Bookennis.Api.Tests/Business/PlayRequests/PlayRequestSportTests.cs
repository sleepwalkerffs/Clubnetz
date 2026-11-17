using Bookennis.Api.Business.PlayRequests;
using Bookennis.Api.Tests.Business.CourtSales;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.PlayRequests;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.PlayRequests;

/// <summary>Court 1 is a tennis court, court 2 a padel court; members are enrolled per sport.</summary>
public class PlayRequestSportTests : TestBase
{
    private readonly IBookingDomainService bookingService;

    public PlayRequestSportTests(TestFixture fixture) : base(fixture)
    {
        bookingService = PlayRequestSeed.BookingServiceBooksUnlessOccupied(GetInstance<IBookingDomainService>());
        GetInstance<IPlayRequestPushNotifier>().ClearSubstitute();
    }

    [Fact]
    public async Task CreatePlayRequest_CourtsOfDifferentSports_Throws()
    {
        var (alice, _, _, bob) = await SeedPlayers();

        var act = () => SendAsync(Create(alice, [bob], [TestDataSeed.Court1Id, TestDataSeed.Court2Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestCourtsOfDifferentSports));
    }

    [Fact]
    public async Task CreatePlayRequest_AsksAMemberEnrolledForAnotherSport_ThrowsWithTheName()
    {
        var (alice, _, _, bob) = await SeedPlayers();

        var act = () => SendAsync(Create(alice, [bob], [TestDataSeed.Court2Id]));

        var error = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        error.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotEnrolledForSport));
        error.ErrorDetails.Should().Equal("bob Player", "Padel");
        (await QueryAsync(ctx => ctx.PlayRequests.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CreatePlayRequest_OnTheTennisCourt_PassesTheSportsOfTheEnrollmentToTheRules()
    {
        var (alice, _, _, bob) = await SeedPlayers();

        await SendAsync(Create(alice, [bob], [TestDataSeed.Court1Id]));

        bookingService.Received(1).BookCourt(
            Arg.Is<BookingDomainService.Context>(c => c.Participants.All(p => p.IsAllowedInCurrentSeason && p.EnrolledSports.Count == 0)),
            Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task AcceptPlayRequest_EnrolledForAnotherSport_Throws()
    {
        var (_, aliceMember, bob, bobMember) = await SeedPlayers();
        var requestId = await QueryAsync(async ctx => (await PlayRequestSeed.AddRequest(ctx, aliceMember, [bobMember], courtIds: [TestDataSeed.Court2Id])).Id);

        var act = () => SendAsync(new AcceptPlayRequest(TestDataSeed.ClubId, bob, requestId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestMemberNotEnrolledForSport));
    }

    [Fact]
    public async Task GetPlayRequestCandidates_ForPadel_LeavesOutMembersEnrolledForTennisOnly()
    {
        var (alice, _, _, bobMember) = await SeedPlayers();

        var all = await SendAsync(new GetPlayRequestCandidates(TestDataSeed.ClubId, alice));
        var padel = await SendAsync(new GetPlayRequestCandidates(TestDataSeed.ClubId, alice, Sport.Padel));

        all.Select(c => c.MemberId).Should().Contain(bobMember);
        padel.Select(c => c.MemberId).Should().NotContain(bobMember);
    }

    /// <summary>Alice is enrolled for every sport, Bob for tennis only; court 2 is a padel court.</summary>
    private Task<(int Alice, int AliceMember, int Bob, int BobMember)> SeedPlayers() => QueryAsync(async ctx =>
    {
        var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
        (await ctx.Courts.SingleAsync(c => c.Id == TestDataSeed.Court2Id)).SetSport(Sport.Padel);

        var (alice, aliceMember) = await PlayRequestSeed.AddPlayer(ctx, "alice");
        var (bob, bobMember) = await PlayRequestSeed.AddPlayer(ctx, "bob");
        ctx.MemberSeasons.Add(new MemberSeason(aliceMember, seasonId));
        ctx.MemberSeasons.Add(new MemberSeason(bobMember, seasonId, sports: [Sport.Tennis]));
        await ctx.SaveChangesAsync();
        return (alice, aliceMember, bob, bobMember);
    });

    private CreatePlayRequest Create(int userId, int[] invitees, int[] courts)
    {
        var start = PlayRequestSeed.Start;
        return new CreatePlayRequest(
            TestDataSeed.ClubId,
            userId,
            Query(ctx => ctx.PlayModes.First(p => p.ClubId == TestDataSeed.ClubId).Id),
            new DateTimeOffsetInterval(start, start.AddMinutes(90)),
            TimeZoneInfo.Utc.Id,
            [.. courts],
            null,
            [.. invitees],
            null,
            null);
    }
}
