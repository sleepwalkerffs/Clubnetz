using Bookennis.Domain.Exceptions;
using Bookennis.Domain.PlayRequests;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.PlayRequests;

public class PlayRequestTests
{
    private const int Requester = 1;
    private static readonly DateTimeOffset Now = new(2037, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = Now.AddDays(2);

    private static PlayRequest.RequestData Data(
        int playersNeeded = 2,
        int[]? invitees = null,
        int[]? courts = null,
        DateTimeOffset? expiresAt = null,
        string? comment = null,
        DateTimeOffset? start = null)
        => new(
            PlayModeId: 5,
            Interval: new DateTimeOffsetInterval(start ?? Start, (start ?? Start).AddHours(1)),
            TimeZoneInfoId: "UTC",
            CourtIds: courts ?? [10, 11],
            PlayersNeeded: playersNeeded,
            InviteeMemberIds: invitees ?? [2, 3],
            Comment: comment,
            ExpiresAt: expiresAt);

    private static PlayRequest Create(PlayRequest.RequestData? data = null) => PlayRequest.Create(7, Requester, data ?? Data(), Now);

    [Fact]
    public void Create_StartsOpen_ExpiresAtStart_AndAsksEveryone()
    {
        var request = Create();

        request.Status.Should().Be(PlayRequestStatus.Open);
        request.ExpiresAt.Should().Be(Start);
        request.ClubId.Should().Be(7);
        request.Invitees.Select(i => i.MemberId).Should().BeEquivalentTo([2, 3]);
        request.Invitees.Should().OnlyContain(i => i.Status == PlayRequestInviteeStatus.Invited);
        request.OpenSpots.Should().Be(1);
    }

    [Fact]
    public void Create_RequesterAndDuplicatesAreLeftOutOfTheInvitees()
    {
        var request = Create(Data(invitees: [Requester, 2, 2, 3]));

        request.Invitees.Select(i => i.MemberId).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void Create_EarlierExpiry_IsKept()
    {
        var request = Create(Data(expiresAt: Start.AddHours(-5)));

        request.ExpiresAt.Should().Be(Start.AddHours(-5));
    }

    [Theory]
    [InlineData(-1, PlayRequest.ErrorCode.PlayRequestInvalidExpiry)] // before now
    [InlineData(1, PlayRequest.ErrorCode.PlayRequestInvalidExpiry)] // after the start
    public void Create_InvalidExpiry_Throws(int hoursFromStart, PlayRequest.ErrorCode expected)
    {
        var expiresAt = hoursFromStart < 0 ? Now.AddMinutes(-1) : Start.AddHours(hoursFromStart);

        var act = () => Create(Data(expiresAt: expiresAt));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(expected.ToString());
    }

    [Fact]
    public void Create_InThePast_Throws()
    {
        var act = () => Create(Data(start: Now.AddMinutes(-5)));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestInvalidStart));
    }

    [Fact]
    public void Create_NoCourts_Throws()
    {
        var act = () => Create(Data(courts: []));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNoCourts));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void Create_InvalidPlayerCount_Throws(int playersNeeded)
    {
        var act = () => Create(Data(playersNeeded: playersNeeded));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestInvalidPlayerCount));
    }

    [Fact]
    public void Create_FewerInviteesThanMissingPlayers_Throws()
    {
        var act = () => Create(Data(playersNeeded: 4, invitees: [2, 3]));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNoInvitees));
    }

    [Fact]
    public void Create_OnlyTheRequesterAsInvitee_Throws()
    {
        var act = () => Create(Data(invitees: [Requester]));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNoInvitees));
    }

    [Fact]
    public void Create_TooManyInvitees_Throws()
    {
        var act = () => Create(Data(invitees: [.. Enumerable.Range(2, PlayRequest.MaxInvitees + 1)]));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestTooManyInvitees));
    }

    [Fact]
    public void Create_CommentTooLong_Throws()
    {
        var act = () => Create(Data(comment: new string('x', PlayRequest.MaxCommentLength + 1)));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestCommentTooLong));
    }

    [Fact]
    public void Accept_Singles_FillsTheLastSpot()
    {
        var request = Create();

        var isFull = request.Accept(2, Now);

        isFull.Should().BeTrue();
        request.OpenSpots.Should().Be(0);
        request.PlayerMemberIds().Should().BeEquivalentTo([Requester, 2]);
    }

    [Fact]
    public void Accept_Doubles_IsFullOnlyWhenTheLastSpotIsTaken()
    {
        var request = Create(Data(playersNeeded: 4, invitees: [2, 3, 4, 5]));

        request.Accept(2, Now).Should().BeFalse();
        request.Accept(3, Now).Should().BeFalse();
        request.OpenSpots.Should().Be(1);
        request.Accept(5, Now).Should().BeTrue();
        request.PlayerMemberIds().Should().BeEquivalentTo([Requester, 2, 3, 5]);
    }

    [Fact]
    public void Accept_Twice_Throws()
    {
        var request = Create(Data(playersNeeded: 4, invitees: [2, 3, 4]));
        request.Accept(2, Now);

        var act = () => request.Accept(2, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestAlreadyAccepted));
    }

    [Fact]
    public void Accept_NotInvited_Throws()
    {
        var act = () => Create().Accept(99, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotInvited));
    }

    [Fact]
    public void Accept_OwnRequest_Throws()
    {
        var act = () => Create().Accept(Requester, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestOwnRequest));
    }

    [Fact]
    public void Accept_AfterTheExpiry_Throws()
    {
        var request = Create(Data(expiresAt: Start.AddHours(-1)));

        var act = () => request.Accept(2, Start.AddHours(-1));

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestExpired));
    }

    [Fact]
    public void Accept_FilledRequest_Throws()
    {
        var request = Create();
        request.Accept(2, Now);
        request.MarkFilled(42, Now);

        var act = () => request.Accept(3, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotOpen));
        request.Status.Should().Be(PlayRequestStatus.Filled);
        request.BookingId.Should().Be(42);
    }

    [Fact]
    public void Decline_FreesTheSpotOfSomebodyWhoAccepted()
    {
        var request = Create(Data(playersNeeded: 4, invitees: [2, 3, 4]));
        request.Accept(2, Now);

        request.Decline(2, Now);

        request.OpenSpots.Should().Be(3);
        request.Invitees.Single(i => i.MemberId == 2).Status.Should().Be(PlayRequestInviteeStatus.Declined);
    }

    [Fact]
    public void Decline_NotInvited_Throws()
    {
        var act = () => Create().Decline(99, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotInvited));
    }

    [Fact]
    public void Cancel_ByRequester_ClosesTheRequest()
    {
        var request = Create();

        request.Cancel(Requester, Now);

        request.Status.Should().Be(PlayRequestStatus.Cancelled);
        request.IsOpenAt(Now).Should().BeFalse();
    }

    [Fact]
    public void Cancel_ByInvitee_Throws()
    {
        var act = () => Create().Cancel(2, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotRequester));
    }

    [Fact]
    public void Cancel_FilledRequest_Throws()
    {
        var request = Create();
        request.Accept(2, Now);
        request.MarkFilled(1, Now);

        var act = () => request.Cancel(Requester, Now);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayRequest.ErrorCode.PlayRequestNotOpen));
    }

    [Fact]
    public void IsOpenAt_IsFalseFromTheExpiry()
    {
        var request = Create(Data(expiresAt: Start.AddHours(-1)));

        request.IsOpenAt(Start.AddHours(-2)).Should().BeTrue();
        request.IsOpenAt(Start.AddHours(-1)).Should().BeFalse();
    }
}

public class PlayerGroupTests
{
    [Fact]
    public void Create_NamesAndKeepsThePlayers_WithoutTheOwner()
    {
        var group = new PlayerGroup(7, 1, "  Tuesday  ", [1, 2, 3, 3]);

        group.Name.Should().Be("Tuesday");
        group.Members.Select(m => m.MemberId).Should().BeEquivalentTo([2, 3]);
        group.OwnerMemberId.Should().Be(1);
    }

    [Fact]
    public void Update_ReplacesThePlayers()
    {
        var group = new PlayerGroup(7, 1, "Tuesday", [2, 3]);

        group.Update("Wednesday", [3, 4]);

        group.Name.Should().Be("Wednesday");
        group.Members.Select(m => m.MemberId).Should().BeEquivalentTo([3, 4]);
    }

    [Theory]
    [InlineData("", nameof(PlayerGroup.ErrorCode.PlayerGroupNameRequired))]
    [InlineData("   ", nameof(PlayerGroup.ErrorCode.PlayerGroupNameRequired))]
    public void Create_WithoutName_Throws(string name, string code)
    {
        var act = () => new PlayerGroup(7, 1, name, [2]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(code);
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var act = () => new PlayerGroup(7, 1, new string('x', PlayerGroup.MaxNameLength + 1), [2]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayerGroup.ErrorCode.PlayerGroupNameTooLong));
    }

    [Fact]
    public void Create_Empty_Throws()
    {
        var act = () => new PlayerGroup(7, 1, "Nobody", [1]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayerGroup.ErrorCode.PlayerGroupEmpty));
    }

    [Fact]
    public void Create_TooManyPlayers_Throws()
    {
        var act = () => new PlayerGroup(7, 1, "Everybody", [.. Enumerable.Range(2, PlayerGroup.MaxMembers + 1)]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(PlayerGroup.ErrorCode.PlayerGroupTooManyMembers));
    }
}
