using Bookennis.Api.Business.Events;
using Bookennis.Api.Business.Members.DomainEventHandlers;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Bookennis.Domain.Members.Events;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.Mediator;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members.DomainEventHandlers;

public class SendMemberSeasonActivatedEmailHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Handle_NoPreviousSeason_SendsWelcomeToClubEmail()
    {
        var (memberId, seasonId) = await SetupSingleSeason();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            var domainEvent = CreateDomainEvent(memberId, seasonId);

            await handler.Handle(domainEvent, CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e => e.Type == ClubEmailType.Welcome && e.Variables is WelcomeEmailVariables),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MemberNotActiveInPreviousSeason_SendsWelcomeToClubEmail()
    {
        var (memberId, currentSeasonId) = await SetupTwoSeasonsWithoutPreviousMembership();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            var domainEvent = CreateDomainEvent(memberId, currentSeasonId);

            await handler.Handle(domainEvent, CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e => e.Type == ClubEmailType.Welcome && e.Variables is WelcomeEmailVariables),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MemberActiveInPreviousSeason_SendsSeasonActivatedEmail()
    {
        var (memberId, currentSeasonId) = await SetupTwoSeasonsWithPreviousMembership();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            var domainEvent = CreateDomainEvent(memberId, currentSeasonId);

            await handler.Handle(domainEvent, CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e => e.Type == ClubEmailType.SeasonActivated && e.Variables is SeasonActivatedEmailVariables),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WelcomeEmail_ContainsCorrectFirstNameAndClubName()
    {
        var (memberId, seasonId) = await SetupSingleSeason();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            var domainEvent = CreateDomainEvent(memberId, seasonId);

            await handler.Handle(domainEvent, CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Variables is WelcomeEmailVariables
                && ((WelcomeEmailVariables)e.Variables).Member.FirstName == "us"
                && ((WelcomeEmailVariables)e.Variables).Season.From == "01.04.2025"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SeasonActivatedEmail_ContainsCorrectData()
    {
        var (memberId, currentSeasonId) = await SetupTwoSeasonsWithPreviousMembership();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            var domainEvent = CreateDomainEvent(memberId, currentSeasonId);

            await handler.Handle(domainEvent, CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Variables is SeasonActivatedEmailVariables
                && ((SeasonActivatedEmailVariables)e.Variables).Member.FirstName == "us"
                && ((SeasonActivatedEmailVariables)e.Variables).Season.From == "01.04.2025"),
            Arg.Any<CancellationToken>());
    }

    private SendMemberSeasonActivatedEmailHandler CreateHandler(IMediator mockMediator)
        => new(
            GetInstance<AppDbContext>(),
            mockMediator);

    private static DomainEvent<MemberActivatedForSeasonDomainEvent> CreateDomainEvent(int memberId, int seasonId)
        => new(typeof(MemberSeason).GUID, 0, new MemberActivatedForSeasonDomainEvent(memberId, seasonId));

    private async Task<(int MemberId, int SeasonId)> SetupSingleSeason()
    {
        return await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member = ctx.TestData().Member1;
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2025, 4, 1), new DateOnly(2025, 10, 31)));
            await ctx.SaveChangesAsync();
            return (member.Id, season.Id);
        });
    }

    private async Task<(int MemberId, int CurrentSeasonId)> SetupTwoSeasonsWithoutPreviousMembership()
    {
        return await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member = ctx.TestData().Member1;
            club.AddSeason(new DateOnlyInterval(new DateOnly(2024, 4, 1), new DateOnly(2024, 10, 31)));
            var currentSeason = club.AddSeason(new DateOnlyInterval(new DateOnly(2025, 4, 1), new DateOnly(2025, 10, 31)));
            await ctx.SaveChangesAsync();
            return (member.Id, currentSeason.Id);
        });
    }

    private async Task<(int MemberId, int CurrentSeasonId)> SetupTwoSeasonsWithPreviousMembership()
    {
        return await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member = ctx.TestData().Member1;
            var previousSeason = club.AddSeason(new DateOnlyInterval(new DateOnly(2024, 4, 1), new DateOnly(2024, 10, 31)));
            var currentSeason = club.AddSeason(new DateOnlyInterval(new DateOnly(2025, 4, 1), new DateOnly(2025, 10, 31)));
            await ctx.SaveChangesAsync();

            ctx.MemberSeasons.Add(new MemberSeason(member.Id, previousSeason.Id));
            await ctx.SaveChangesAsync();

            return (member.Id, currentSeason.Id);
        });
    }
}
