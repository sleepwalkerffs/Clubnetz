using Bookennis.Api.Business.PlayRequests;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.PlayRequests;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.PlayRequests;

public class PlayerGroupHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CreatePlayerGroup_SavesTheGroupForTheCaller_WithNamesSortedByLastName()
    {
        var (aliceUser, alice) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (_, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));

        var group = await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Tuesday", [bob, TestDataSeed.Member2Id]));

        group.Name.Should().Be("Tuesday");
        group.Members.Select(m => m.MemberId).Should().BeEquivalentTo([bob, TestDataSeed.Member2Id]);
        group.Members.Select(m => m.LastName).Should().Equal("min", "Player");
        var saved = await QueryAsync(ctx => ctx.PlayerGroups.SingleAsync());
        saved.OwnerMemberId.Should().Be(alice);
    }

    [Fact]
    public async Task GetPlayerGroups_OnlyReturnsTheGroupsOfTheCaller()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Alice's", [TestDataSeed.Member2Id]));
        await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, bobUser, "Bob's", [TestDataSeed.Member2Id]));

        var result = await SendAsync(new GetPlayerGroups(TestDataSeed.ClubId, aliceUser));

        result.Should().ContainSingle().Which.Name.Should().Be("Alice's");
    }

    [Fact]
    public async Task UpdatePlayerGroup_RenamesAndReplacesThePlayers()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (_, bob) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var group = await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Tuesday", [TestDataSeed.Member2Id]));

        var updated = await SendAsync(new UpdatePlayerGroup(TestDataSeed.ClubId, aliceUser, group.Id, "Wednesday", [bob]));

        updated.Name.Should().Be("Wednesday");
        updated.Members.Should().ContainSingle().Which.MemberId.Should().Be(bob);
        (await QueryAsync(ctx => ctx.PlayerGroupMembers.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task UpdatePlayerGroup_GroupOfSomebodyElse_IsNotFound()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var group = await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Tuesday", [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new UpdatePlayerGroup(TestDataSeed.ClubId, bobUser, group.Id, "Mine now", [TestDataSeed.Member2Id]));

        await act.Should().ThrowAsync<EntityNotFoundException>();
        (await QueryAsync(ctx => ctx.PlayerGroups.SingleAsync())).Name.Should().Be("Tuesday");
    }

    [Fact]
    public async Task DeletePlayerGroup_RemovesTheGroupAndItsPlayers()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var group = await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Tuesday", [TestDataSeed.Member2Id]));

        await SendAsync(new DeletePlayerGroup(TestDataSeed.ClubId, aliceUser, group.Id));

        (await QueryAsync(ctx => ctx.PlayerGroups.CountAsync())).Should().Be(0);
        (await QueryAsync(ctx => ctx.PlayerGroupMembers.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task DeletePlayerGroup_GroupOfSomebodyElse_IsNotFoundAndKept()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        var (bobUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "bob"));
        var group = await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Tuesday", [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new DeletePlayerGroup(TestDataSeed.ClubId, bobUser, group.Id));

        await act.Should().ThrowAsync<EntityNotFoundException>();
        (await QueryAsync(ctx => ctx.PlayerGroups.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task CreatePlayerGroup_PlayerThatDoesNotExistInTheClub_Throws()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        var act = () => SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "Ghosts", [999_999]));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CreatePlayerGroup_TooManyGroups_Throws()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));
        for (var i = 0; i < PlayerGroup.MaxGroupsPerMember; i++)
            await SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, $"Group {i}", [TestDataSeed.Member2Id]));

        var act = () => SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, "One too many", [TestDataSeed.Member2Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayerGroup.ErrorCode.PlayerGroupTooMany));
    }

    [Fact]
    public async Task CreatePlayerGroup_WithoutAName_Throws()
    {
        var (aliceUser, _) = await QueryAsync(ctx => PlayRequestSeed.AddPlayer(ctx, "alice"));

        var act = () => SendAsync(new CreatePlayerGroup(TestDataSeed.ClubId, aliceUser, " ", [TestDataSeed.Member2Id]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(PlayerGroup.ErrorCode.PlayerGroupNameRequired));
    }
}
