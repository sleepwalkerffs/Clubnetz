using Bookennis.Api.Business.Badges.DomainEventHandlers;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.Members.Events;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.Mediator;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges.DomainEventHandlers;

public class SendMemberBadgeAwardedEmailHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Handle_MemberHasEmail_SendsEmailWithBadgeDetails()
    {
        var (memberId, tierId, seasonId) = await SeedBadgeTier(withImage: false);

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(memberId, tierId, seasonId), CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Type == ClubEmailType.BadgeAwarded
                && e.Variables is BadgeAwardedEmailVariables
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.Name == "Bronze"
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.Description == "Bronze badge"
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.ImageUrl == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BadgeHasImage_IncludesPublicImageUrl()
    {
        var (memberId, tierId, seasonId) = await SeedBadgeTier(withImage: true);

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(memberId, tierId, seasonId), CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Type == ClubEmailType.BadgeAwarded
                && e.Variables is BadgeAwardedEmailVariables
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.ImageUrl != null
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.ImageUrl!.Contains($"/BadgeTiers/{tierId}/public-image")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MemberIsChildWithoutEmail_SendsEmailToParent()
    {
        var (childMemberId, tierId, seasonId, parentEmail) = await SeedChildWithParent();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(childMemberId, tierId, seasonId), CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e => e.Recipient == parentEmail),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoEmailAndNoParent_DoesNotSendEmail()
    {
        var (memberId, tierId, seasonId) = await SeedMemberWithoutEmail();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(memberId, tierId, seasonId), CancellationToken.None);
        });

        await mockMediator.DidNotReceive().Send(Arg.Any<SendClubEmail>(), Arg.Any<CancellationToken>());
    }

    private SendMemberBadgeAwardedEmailHandler CreateHandler(IMediator mockMediator)
        => new(
            GetInstance<AppDbContext>(),
            mockMediator,
            GetInstance<AppSettings>());

    private static DomainEvent<MemberBadgeAwardedDomainEvent> CreateDomainEvent(int memberId, int badgeTierId, int seasonId)
        => new(typeof(MemberBadge).GUID, 0, new MemberBadgeAwardedDomainEvent(memberId, badgeTierId, seasonId));

    private async Task<(int MemberId, int TierId, int SeasonId)> SeedBadgeTier(bool withImage)
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var tier = new BadgeTier(clubId, season.Id, 1, "Bronze", "Bronze badge", 3, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();

            if (withImage)
            {
                ctx.Add(new BadgeTierImage(tier.Id, [1, 2, 3], "image/jpeg"));
                await ctx.SaveChangesAsync();
            }

            return (testData.Member1.Id, tier.Id, season.Id);
        });
    }

    private async Task<(int ChildMemberId, int TierId, int SeasonId, string ParentEmail)> SeedChildWithParent()
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;

            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var tier = new BadgeTier(clubId, season.Id, 1, "Bronze", "Bronze badge", 3, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();

            var parentUser = new User("parent@test.com", "parent@test.com", "Parent", "Test", new DateOnly(1980, 1, 1), Gender.Female);
            ctx.Add(parentUser);
            await ctx.SaveChangesAsync();
            var parentMember = new ClubMember(parentUser.Id, clubId, [MemberRole.User]);
            ctx.Add(parentMember);
            await ctx.SaveChangesAsync();

            var childUser = new User("Child", "Test", new DateOnly(2012, 1, 1), Gender.Male, parentUser.Id);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();
            var childMember = new ClubMember(childUser.Id, clubId, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            var family = new Family(clubId, [parentMember.Id], [childMember.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            return (childMember.Id, tier.Id, season.Id, "parent@test.com");
        });
    }

    private async Task<(int MemberId, int TierId, int SeasonId)> SeedMemberWithoutEmail()
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;

            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            var tier = new BadgeTier(clubId, season.Id, 1, "Bronze", "Bronze badge", 3, 1);
            ctx.Add(tier);

            var parentUser = new User("orphanparent@test.com", "orphanparent@test.com", "Orphan", "Parent", new DateOnly(1980, 1, 1), Gender.Female);
            ctx.Add(parentUser);
            await ctx.SaveChangesAsync();

            var user = new User("NoEmail", "Member", new DateOnly(2012, 1, 1), Gender.Male, parentUser.Id);
            ctx.Add(user);
            await ctx.SaveChangesAsync();
            var member = new ClubMember(user.Id, clubId, [MemberRole.User]);
            ctx.Add(member);
            await ctx.SaveChangesAsync();

            return (member.Id, tier.Id, season.Id);
        });
    }
}
