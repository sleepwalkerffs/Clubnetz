using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SharedClubEmailType = Bookennis.Shared.Controller.ClubEmailTemplates.ClubEmailType;
using SharedLanguage = Bookennis.Shared.Controller.Shared.Language;

namespace Bookennis.Api.Tests.Business.ClubEmails;

public class ClubEmailTemplatesTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubEmailTemplates_ReturnsAllTypesWithCustomizedLanguagesAndSettings()
    {
        var clubId = await SeedTemplate(ClubEmailType.Welcome, Language.German, "Hallo", "Hallo {{ member.first_name }}");
        await SendAsync(new UpdateClubEmailSettings(clubId, "https://tc-test.at", "office@tc-test.at"));

        var result = await SendAsync(new GetClubEmailTemplates(clubId));

        result.Templates.Select(t => t.Type).Should().BeEquivalentTo(Enum.GetValues<SharedClubEmailType>());
        result.Templates.Single(t => t.Type == SharedClubEmailType.Welcome).CustomizedLanguages.Should().Equal(SharedLanguage.German);
        result.Templates.Single(t => t.Type == SharedClubEmailType.GuestCard).CustomizedLanguages.Should().BeEmpty();
        result.Settings.WebsiteUrl.Should().Be("https://tc-test.at");
        result.Settings.ReplyToEmail.Should().Be("office@tc-test.at");
    }

    [Fact]
    public async Task GetClubEmailTemplate_ReturnsCustomAndDefaultLanguages()
    {
        var clubId = await SeedTemplate(ClubEmailType.Welcome, Language.German, "Hallo", "Hallo {{ member.first_name }}");

        var result = await SendAsync(new GetClubEmailTemplate(clubId, ClubEmailType.Welcome));

        var german = result.Languages.Single(l => l.Language == SharedLanguage.German);
        german.IsCustomized.Should().BeTrue();
        german.Subject.Should().Be("Hallo");
        german.DefaultSubject.Should().Be("Willkommen im Verein!");

        var english = result.Languages.Single(l => l.Language == SharedLanguage.English);
        english.IsCustomized.Should().BeFalse();
        english.Subject.Should().Be("Welcome to the Club!");
        english.Body.Should().Be(english.DefaultBody);
        english.Variables.Select(v => v.Name).Should().Contain(["member.first_name", "club.name", "season.period"]);
        english.Variables.Single(v => v.Name == "club.name").SampleValue.Should().Be("TestClub");
    }

    [Fact]
    public async Task UpdateClubEmailTemplate_CreatesUpdatesAndRemovesLanguages()
    {
        var clubId = await SeedTemplate(ClubEmailType.SeasonActivated, Language.German, "Alt", "Alt");

        await SendAsync(new UpdateClubEmailTemplate(clubId, ClubEmailType.SeasonActivated,
        [
            new UpdateClubEmailTemplate.LanguageContent(Language.English, "Welcome back", "Hi {{ member.first_name }}")
        ]));

        var templates = await QueryAsync(ctx => ctx.ClubEmailTemplates.Where(t => t.ClubId == clubId).ToListAsync());
        templates.Should().ContainSingle();
        templates[0].Language.Should().Be(Language.English);
        templates[0].Subject.Should().Be("Welcome back");

        await SendAsync(new UpdateClubEmailTemplate(clubId, ClubEmailType.SeasonActivated,
        [
            new UpdateClubEmailTemplate.LanguageContent(Language.English, "Welcome back!", "Hi {{ member.full_name }}"),
            new UpdateClubEmailTemplate.LanguageContent(Language.German, "Willkommen zurück", "Hallo {{ member.first_name }}")
        ]));

        templates = await QueryAsync(ctx => ctx.ClubEmailTemplates.Where(t => t.ClubId == clubId).OrderBy(t => t.Language).ToListAsync());
        templates.Select(t => t.Subject).Should().Equal("Willkommen zurück", "Welcome back!");
    }

    [Fact]
    public async Task UpdateClubEmailTemplate_UnknownVariable_Throws()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var act = () => SendAsync(new UpdateClubEmailTemplate(clubId, ClubEmailType.GuestCard,
        [
            new UpdateClubEmailTemplate.LanguageContent(Language.English, "Guest", "{{ season.period }}")
        ]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ClubEmailTemplateErrorCode.UnknownEmailTemplateVariable));
        (await QueryAsync(ctx => ctx.ClubEmailTemplates.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateClubEmailTemplate_DuplicateLanguage_Throws()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var act = () => SendAsync(new UpdateClubEmailTemplate(clubId, ClubEmailType.GuestCard,
        [
            new UpdateClubEmailTemplate.LanguageContent(Language.English, "A", "A"),
            new UpdateClubEmailTemplate.LanguageContent(Language.English, "B", "B")
        ]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(UpdateClubEmailTemplate.ErrorCode.DuplicateEmailTemplateLanguage));
    }

    [Fact]
    public async Task ResetClubEmailTemplate_RemovesOnlyTemplatesOfType()
    {
        var clubId = await SeedTemplate(ClubEmailType.Welcome, Language.German, "Hallo", "Hallo");
        await SeedTemplate(ClubEmailType.GuestCard, Language.German, "Gast", "Gast");

        await SendAsync(new ResetClubEmailTemplate(clubId, ClubEmailType.Welcome));

        var types = await QueryAsync(ctx => ctx.ClubEmailTemplates.Select(t => t.Type).ToListAsync());
        types.Should().Equal(ClubEmailType.GuestCard);
    }

    [Fact]
    public async Task PreviewClubEmailTemplate_RendersWithSampleDataAndClub()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var result = await SendAsync(new PreviewClubEmailTemplate(clubId, ClubEmailType.BadgeAwarded, Language.English,
            "Badge for {{ member.first_name }}", "**{{ badge.name }}** at {{ club.name }}"));

        result.Subject.Should().Be("Badge for Max");
        result.Html.Should().Contain("<strong>Gold</strong> at TestClub");
    }

    [Fact]
    public async Task PreviewClubEmailTemplate_InvalidTemplate_Throws()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var act = () => SendAsync(new PreviewClubEmailTemplate(clubId, ClubEmailType.BadgeAwarded, Language.English, "Subject", "{% if %}"));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(ClubEmailTemplateErrorCode.InvalidEmailTemplateSyntax));
    }

    [Fact]
    public async Task UpdateClubEmailSettings_EmptyValues_ClearSettings()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        await SendAsync(new UpdateClubEmailSettings(clubId, " https://tc-test.at ", "office@tc-test.at"));
        await SendAsync(new UpdateClubEmailSettings(clubId, "", null));

        var club = await QueryAsync(ctx => ctx.Clubs.SingleAsync(c => c.Id == clubId));
        club.WebsiteUrl.Should().BeNull();
        club.ReplyToEmail.Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)", null, nameof(UpdateClubEmailSettings.ErrorCode.InvalidWebsiteUrl))]
    [InlineData("www.no-scheme.at", null, nameof(UpdateClubEmailSettings.ErrorCode.InvalidWebsiteUrl))]
    [InlineData(null, "not-an-email", nameof(UpdateClubEmailSettings.ErrorCode.InvalidReplyToEmail))]
    public async Task UpdateClubEmailSettings_InvalidValues_Throw(string? websiteUrl, string? replyToEmail, string errorCode)
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var act = () => SendAsync(new UpdateClubEmailSettings(clubId, websiteUrl, replyToEmail));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(errorCode);
    }

    private Task<int> SeedTemplate(ClubEmailType type, Language language, string subject, string body)
        => QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            ctx.ClubEmailTemplates.Add(new ClubEmailTemplate(clubId, type, language, subject, body));
            await ctx.SaveChangesAsync();
            return clubId;
        });
}
