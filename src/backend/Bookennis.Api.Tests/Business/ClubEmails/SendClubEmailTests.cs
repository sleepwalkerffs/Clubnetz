using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.ClubEmails.EmailViewModels;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.Email;
using Fusonic.Extensions.Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEmails;

public class SendClubEmailTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly SeasonActivatedEmailVariables Variables = new(new MemberVariables("Anna", "Ace"), new SeasonVariables("01.04.2025", "31.10.2025"));

    [Fact]
    public async Task Handle_NoClubTemplate_SendsDefaultTemplateWithClubValues()
    {
        var clubId = await SetupClubSettings("https://tc-test.at", "office@tc-test.at");

        var mediator = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "Anna@Example.com", "Anna Ace", Language.English, Variables));

        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e =>
                e.Recipient == "anna@example.com"
                && e.RecipientDisplayName == "Anna Ace"
                && e.ReplyTo == "office@tc-test.at"
                && e.SubjectKey == "Your membership has been activated"
                && ((ClubEmailViewModel)e.ViewModel).Title == "Your membership has been activated"
                && ((ClubEmailViewModel)e.ViewModel).BodyHtml.Contains("Dear Anna")
                && ((ClubEmailViewModel)e.ViewModel).BodyHtml.Contains("TestClub")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClubTemplateInLanguage_IsUsed()
    {
        var clubId = await SeedTemplates(
            (Language.German, "Hallo {{ member.first_name }}", "Deutsch"),
            (Language.English, "Hello {{ member.first_name }}", "English"));

        var mediator = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "anna@example.com", "Anna Ace", Language.German, Variables));

        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e => e.SubjectKey == "Hallo Anna" && ((ClubEmailViewModel)e.ViewModel).BodyHtml.Contains("Deutsch")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClubTemplateOnlyInOtherLanguage_IsUsed()
    {
        var clubId = await SeedTemplates((Language.English, "Hello {{ member.first_name }}", "English"));

        var mediator = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "anna@example.com", "Anna Ace", Language.German, Variables));

        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e => e.SubjectKey == "Hello Anna"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TemplateOverride_IsUsedAndBracesInSubjectAreEscaped()
    {
        var clubId = await SeedTemplates((Language.English, "Saved", "Saved"));

        var mediator = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "anna@example.com", "Anna Ace", Language.English, Variables,
            new ClubEmailTemplateContent("{Test} {{ member.first_name }}", "Override")));

        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e =>
                e.SubjectKey == "{{Test}} Anna"
                && ((ClubEmailViewModel)e.ViewModel).Title == "{Test} Anna"
                && ((ClubEmailViewModel)e.ViewModel).BodyHtml.Contains("Override")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BrokenSavedTemplate_FallsBackToDefault()
    {
        // Can not be saved through UpdateClubEmailTemplate, but must never prevent the email from being sent.
        var clubId = await SeedTemplates((Language.English, "Broken", "{% if member.first_name %}"));

        var mediator = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "anna@example.com", "Anna Ace", Language.English, Variables));

        await mediator.Received(1).Send(
            Arg.Is<SendEmail>(e => e.SubjectKey == "Your membership has been activated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Attachments_ArePassedToTheEmail()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var attachment = new Attachment("flyer.pdf", new Uri("club-announcement-attachment:42"));

        var withAttachment = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "anna@example.com", "Anna Ace", Language.English, Variables, Attachments: [attachment]));
        var without = await Send(new SendClubEmail(clubId, ClubEmailType.SeasonActivated, "anna@example.com", "Anna Ace", Language.English, Variables));

        await withAttachment.Received(1).Send(
            Arg.Is<SendEmail>(e => e.Attachments != null && e.Attachments.Length == 1 && e.Attachments[0] == attachment),
            Arg.Any<CancellationToken>());
        await without.Received(1).Send(Arg.Is<SendEmail>(e => e.Attachments == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendTestClubEmail_SendsUnsavedTemplateToCurrentUser()
    {
        var (clubId, userId) = Query(ctx => (ctx.TestData().Club.Id, ctx.TestData().Admin.Id));
        var userAccessor = Substitute.For<IUserAccessor>();
        userAccessor.TryGetUser(out Arg.Any<ClaimsPrincipal>()!).Returns(x =>
        {
            x[0] = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
            return true;
        });

        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(() => new SendTestClubEmail.Handler(GetInstance<AppDbContext>(), GetInstance<IClubEmailRenderer>(), userAccessor, mediator)
            .Handle(new SendTestClubEmail(clubId, ClubEmailType.GuestCard, Language.English, "Test", "{{ guest_card_url }}"), CancellationToken.None));

        await mediator.Received(1).Send(
            Arg.Is<SendClubEmail>(e =>
                e.Recipient == "admin@bookennis.com"
                && e.Type == ClubEmailType.GuestCard
                && e.Variables is GuestCardEmailVariables
                && e.TemplateOverride != null
                && e.TemplateOverride.Body == "{{ guest_card_url }}"),
            Arg.Any<CancellationToken>());
    }

    private async Task<IMediator> Send(SendClubEmail request)
    {
        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(() => new SendClubEmail.Handler(
                GetInstance<AppDbContext>(),
                mediator,
                GetInstance<IClubEmailRenderer>(),
                GetInstance<AppSettings>(),
                NullLogger<SendClubEmail>.Instance)
            .Handle(request, CancellationToken.None));
        return mediator;
    }

    private Task<int> SetupClubSettings(string? websiteUrl, string? replyToEmail)
        => QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            club.UpdateContactSettings(websiteUrl, replyToEmail);
            await ctx.SaveChangesAsync();
            return club.Id;
        });

    private Task<int> SeedTemplates(params (Language Language, string Subject, string Body)[] templates)
        => QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            foreach (var (language, subject, body) in templates)
                ctx.ClubEmailTemplates.Add(new ClubEmailTemplate(clubId, ClubEmailType.SeasonActivated, language, subject, body));
            await ctx.SaveChangesAsync();
            return clubId;
        });
}
