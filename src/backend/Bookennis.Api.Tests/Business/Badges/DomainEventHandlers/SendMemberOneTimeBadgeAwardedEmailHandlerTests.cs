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

public class SendMemberOneTimeBadgeAwardedEmailHandlerTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Handle_MemberHasEmail_SendsEmailWithBadgeDetails()
    {
        var (memberId, badgeId) = await SeedOneTimeBadge(withImage: false);

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(memberId, badgeId), CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Type == ClubEmailType.BadgeAwarded
                && e.Variables is BadgeAwardedEmailVariables
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.Name == "Singles Champion Men"
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.Description == "Winner of the men's singles tournament"
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.ImageUrl == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BadgeHasImage_IncludesPublicImageUrl()
    {
        var (memberId, badgeId) = await SeedOneTimeBadge(withImage: true);

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(memberId, badgeId), CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Type == ClubEmailType.BadgeAwarded
                && e.Variables is BadgeAwardedEmailVariables
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.ImageUrl != null
                && ((BadgeAwardedEmailVariables)e.Variables).Badge.ImageUrl!.Contains($"/OneTimeBadges/{badgeId}/public-image")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MemberIsChildWithoutEmail_SendsEmailToParent()
    {
        var (childMemberId, badgeId, parentEmail) = await SeedChildWithParent();

        var mockMediator = Substitute.For<IMediator>();
        await ScopedAsync(async () =>
        {
            var handler = CreateHandler(mockMediator);
            await handler.Handle(CreateDomainEvent(childMemberId, badgeId), CancellationToken.None);
        });

        await mockMediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e => e.Recipient == parentEmail),
            Arg.Any<CancellationToken>());
    }

    private SendMemberOneTimeBadgeAwardedEmailHandler CreateHandler(IMediator mockMediator)
        => new(
            GetInstance<AppDbContext>(),
            mockMediator,
            GetInstance<AppSettings>());

    private static DomainEvent<MemberOneTimeBadgeAwardedDomainEvent> CreateDomainEvent(int memberId, int oneTimeBadgeId)
        => new(typeof(MemberOneTimeBadge).GUID, 0, new MemberOneTimeBadgeAwardedDomainEvent(memberId, oneTimeBadgeId));

    private async Task<(int MemberId, int BadgeId)> SeedOneTimeBadge(bool withImage)
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var badge = new OneTimeBadge(clubId, season.Id, "Singles Champion Men", "Winner of the men's singles tournament");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();

            if (withImage)
            {
                ctx.Add(new OneTimeBadgeImage(badge.Id, [1, 2, 3], "image/jpeg"));
                await ctx.SaveChangesAsync();
            }

            return (testData.Member1.Id, badge.Id);
        });
    }

    private async Task<(int ChildMemberId, int BadgeId, string ParentEmail)> SeedChildWithParent()
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;

            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var badge = new OneTimeBadge(clubId, season.Id, "Singles Champion Men", "Winner of the men's singles tournament");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();

            var parentUser = new User("otbparent@test.com", "otbparent@test.com", "Parent", "Test", new DateOnly(1980, 1, 1), Gender.Female);
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

            return (childMember.Id, badge.Id, "otbparent@test.com");
        });
    }
}
