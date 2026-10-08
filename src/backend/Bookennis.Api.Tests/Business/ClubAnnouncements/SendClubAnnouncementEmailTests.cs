using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SharedAudience = Bookennis.Shared.Controller.ClubAnnouncements.ClubAnnouncementAudience;

namespace Bookennis.Api.Tests.Business.ClubAnnouncements;

public class SendClubAnnouncementEmailTests(TestFixture fixture) : TestBase(fixture)
{
    private const string AdminEmail = "admin@bookennis.com";
    private const string UserEmail = "user@bookennis.com";

    [Fact]
    public async Task Recipients_AllMembers_ReturnsEveryMemberWithEmail()
    {
        var recipients = await Resolve(ClubAnnouncementAudience.AllMembers);

        recipients.Select(r => r.Email).Should().BeEquivalentTo(AdminEmail, UserEmail);
        recipients.Single(r => r.Email == UserEmail).Should().Be(new ClubAnnouncementRecipient(UserEmail, "us", "er", Language.German));
    }

    [Fact]
    public async Task Recipients_ChildWithoutEmail_IsReachedOnceViaTheParent()
    {
        await QueryAsync(async ctx =>
        {
            var (_, firstChild) = await MemberSeed.AddChild(ctx, TestDataSeed.UserId, "Anna");
            var (_, secondChild) = await MemberSeed.AddChild(ctx, TestDataSeed.UserId, "Ben");
            await MemberSeed.AddFamily(ctx, [TestDataSeed.Member1Id], [firstChild, secondChild]);
        });

        var all = await Resolve(ClubAnnouncementAudience.AllMembers);
        var youth = await Resolve(ClubAnnouncementAudience.Youth);

        // The parent's address is used for the parent and both children, the parent is the one addressed
        all.Select(r => r.Email).Should().BeEquivalentTo(AdminEmail, UserEmail);
        all.Single(r => r.Email == UserEmail).FirstName.Should().Be("us");

        // Only the children are in the audience, they share the address of their parent
        youth.Should().ContainSingle().Which.Should().Be(new ClubAnnouncementRecipient(UserEmail, "Anna", "Child", Language.German));
    }

    [Fact]
    public async Task Recipients_ActiveSeasonMembers_ReturnsOnlyEnrolledMembers()
    {
        await QueryAsync(async ctx =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var past = new Season(TestDataSeed.ClubId, new DateOnlyInterval(today.AddDays(-400), today.AddDays(-200)));
            var active = new Season(TestDataSeed.ClubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.AddRange(past, active);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(TestDataSeed.Member1Id, past.Id));
            ctx.Add(new MemberSeason(TestDataSeed.Member2Id, active.Id));
            await ctx.SaveChangesAsync();
        });

        var recipients = await Resolve(ClubAnnouncementAudience.ActiveSeasonMembers);

        recipients.Select(r => r.Email).Should().Equal(AdminEmail);
    }

    [Fact]
    public async Task Recipients_ActiveSeasonMembersWithoutActiveSeason_ThrowsPreconditionException()
    {
        var act = () => Resolve(ClubAnnouncementAudience.ActiveSeasonMembers);

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubAnnouncementRecipients.ErrorCode.ClubAnnouncementNoActiveSeason));
    }

    [Fact]
    public async Task Recipients_Roles_ReturnsMembersWithOneOfTheRoles()
    {
        await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "trainer@bookennis.com", MemberRole.Trainer));

        var recipients = await Resolve(ClubAnnouncementAudience.Roles, MemberRole.Admin, MemberRole.Trainer);

        recipients.Select(r => r.Email).Should().BeEquivalentTo(AdminEmail, "trainer@bookennis.com");
    }

    [Fact]
    public async Task Recipients_RolesWithoutRole_ThrowsPreconditionException()
    {
        var act = () => Resolve(ClubAnnouncementAudience.Roles);

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubAnnouncementRecipients.ErrorCode.ClubAnnouncementRolesRequired));
    }

    [Fact]
    public async Task GetClubAnnouncementEmailRecipients_ReturnsTheNumberOfAddresses()
    {
        var all = await SendAsync(new GetClubAnnouncementEmailRecipients(TestDataSeed.ClubId, ClubAnnouncementAudience.AllMembers, []));
        var youth = await SendAsync(new GetClubAnnouncementEmailRecipients(TestDataSeed.ClubId, ClubAnnouncementAudience.Youth, []));

        all.RecipientCount.Should().Be(2);
        youth.RecipientCount.Should().Be(0);
    }

    [Fact]
    public async Task SendClubAnnouncementEmail_SendsOneBatchAndRemembersTheDispatch()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);

        var (mediator, result) = await Send(new SendClubAnnouncementEmail(TestDataSeed.ClubId, id, ClubAnnouncementAudience.Roles, [MemberRole.Admin]));

        await mediator.Received(1).Send(
            Arg.Is<SendClubAnnouncementEmailBatch>(b =>
                b.ClubId == TestDataSeed.ClubId
                && b.ClubAnnouncementId == id
                && b.Recipients.Length == 1
                && b.Recipients[0].Email == AdminEmail),
            Arg.Any<CancellationToken>());

        result.EmailRecipientCount.Should().Be(1);
        result.EmailAudience.Should().Be(SharedAudience.Roles);
        result.EmailSentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var stored = await QueryAsync(ctx => ctx.ClubAnnouncements.SingleAsync(a => a.Id == id));
        stored.EmailRecipientCount.Should().Be(1);
        stored.EmailAudience.Should().Be(ClubAnnouncementAudience.Roles);
    }

    [Fact]
    public async Task SendClubAnnouncementEmail_ManyRecipients_AreSplitIntoBatches()
    {
        var id = await QueryAsync(async ctx =>
        {
            var users = Enumerable.Range(0, SendClubAnnouncementEmail.BatchSize + 5)
                .Select(i => new User($"member{i:000}@bookennis.com", $"member{i:000}@bookennis.com", "Member", $"Nr{i:000}", new DateOnly(1990, 1, 1), Gender.Female))
                .ToList();
            ctx.AddRange(users);
            await ctx.SaveChangesAsync();

            ctx.AddRange(users.Select(u => new ClubMember(u.Id, TestDataSeed.ClubId, [MemberRole.User, MemberRole.Trainer])));
            await ctx.SaveChangesAsync();

            return (await ClubAnnouncementSeed.Seed(ctx)).Id;
        });

        var (mediator, result) = await Send(new SendClubAnnouncementEmail(TestDataSeed.ClubId, id, ClubAnnouncementAudience.Roles, [MemberRole.Trainer]));
        var batches = SentRequests<SendClubAnnouncementEmailBatch>(mediator);

        result.EmailRecipientCount.Should().Be(SendClubAnnouncementEmail.BatchSize + 5);
        batches.Select(b => b.Recipients.Length).Should().Equal(SendClubAnnouncementEmail.BatchSize, 5);
        batches.SelectMany(b => b.Recipients).Select(r => r.Email).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SendClubAnnouncementEmail_NoRecipients_ThrowsAndSendsNothing()
    {
        var id = await QueryAsync(async ctx => (await ClubAnnouncementSeed.Seed(ctx)).Id);
        var mediator = Substitute.For<IMediator>();

        var act = () => ScopedAsync(() => CreateHandler(mediator).Handle(
            new SendClubAnnouncementEmail(TestDataSeed.ClubId, id, ClubAnnouncementAudience.Youth, []), CancellationToken.None));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubAnnouncementRecipients.ErrorCode.ClubAnnouncementNoRecipients));
        await mediator.DidNotReceiveWithAnyArgs().Send(default(SendClubAnnouncementEmailBatch)!, default);

        var stored = await QueryAsync(ctx => ctx.ClubAnnouncements.SingleAsync(a => a.Id == id));
        stored.EmailSentAt.Should().BeNull();
    }

    [Fact]
    public async Task SendClubAnnouncementEmailBatch_SendsTheClubEmailWithAttachmentsToEveryRecipient()
    {
        var (id, attachmentIds) = await QueryAsync(async ctx =>
        {
            var announcement = await ClubAnnouncementSeed.SeedWithAttachments(ctx);
            return (announcement.Id, announcement.Attachments.OrderBy(a => a.Id).Select(a => a.Id).ToList());
        });

        var mediator = await SendBatch(new SendClubAnnouncementEmailBatch(TestDataSeed.ClubId, id,
        [
            new ClubAnnouncementRecipient("anna@example.com", "Anna", "Ace", Language.German),
            new ClubAnnouncementRecipient("ben@example.com", "Ben", "Baseline", Language.English),
        ]));
        var emails = SentRequests<SendClubEmail>(mediator);

        emails.Select(e => (e.Recipient, e.RecipientDisplayName, e.Language)).Should().Equal(
            ("anna@example.com", "Anna Ace", Language.German),
            ("ben@example.com", "Ben Baseline", Language.English));

        var email = emails[0];
        email.ClubId.Should().Be(TestDataSeed.ClubId);
        email.Type.Should().Be(ClubEmailType.Announcement);

        var variables = email.Variables.Should().BeOfType<AnnouncementEmailVariables>().Subject;
        variables.Member.FirstName.Should().Be("Anna");
        variables.Announcement.Title.Should().Be("Summer party");
        variables.Announcement.Body.Value.Should().StartWith("We **celebrate**");
        variables.Announcement.Url.Should().EndWith($"/clubs/{TestDataSeed.ClubId}/news/{id}");

        email.Attachments.Should().NotBeNull();
        email.Attachments!.Select(a => (a.Name, a.Source)).Should().Equal(
            ("flyer.pdf", ClubAnnouncementAttachmentResolver.CreateUri(attachmentIds[0])),
            ("plan.xlsx", ClubAnnouncementAttachmentResolver.CreateUri(attachmentIds[1])));
    }

    [Fact]
    public async Task SendClubAnnouncementEmailBatch_DeletedAnnouncement_SendsNothing()
    {
        var mediator = await SendBatch(new SendClubAnnouncementEmailBatch(TestDataSeed.ClubId, 999_999,
            [new ClubAnnouncementRecipient("anna@example.com", "Anna", "Ace", Language.German)]));

        await mediator.DidNotReceiveWithAnyArgs().Send(default(SendClubEmail)!, default);
    }

    private static List<TRequest> SentRequests<TRequest>(IMediator mediator)
        => mediator.ReceivedCalls().Select(call => call.GetArguments()[0]).OfType<TRequest>().ToList();

    private Task<List<ClubAnnouncementRecipient>> Resolve(ClubAnnouncementAudience audience, params MemberRole[] roles)
        => ScopedAsync(() => ClubAnnouncementRecipients.Resolve(GetInstance<AppDbContext>(), TestDataSeed.ClubId, audience, roles, CancellationToken.None));

    private SendClubAnnouncementEmail.Handler CreateHandler(IMediator mediator)
        => new(GetInstance<AppDbContext>(), mediator, GetInstance<IClubEmailRenderer>());

    private async Task<(IMediator Mediator, Bookennis.Shared.Controller.ClubAnnouncements.ClubAnnouncementDto Result)> Send(SendClubAnnouncementEmail request)
    {
        var mediator = Substitute.For<IMediator>();
        var result = await ScopedAsync(() => CreateHandler(mediator).Handle(request, CancellationToken.None));
        return (mediator, result);
    }

    private async Task<IMediator> SendBatch(SendClubAnnouncementEmailBatch request)
    {
        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(() => new SendClubAnnouncementEmailBatch.Handler(GetInstance<AppDbContext>(), mediator, GetInstance<AppSettings>())
            .Handle(request, CancellationToken.None));
        return mediator;
    }
}
