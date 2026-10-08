using Bookennis.Api.Business.ClubEmails;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEmails;

public class ClubEmailRendererTests
{
    private readonly ClubEmailRenderer renderer = new();

    public static TheoryData<ClubEmailType, Language> AllTypesAndLanguages()
    {
        var data = new TheoryData<ClubEmailType, Language>();
        foreach (var type in Enum.GetValues<ClubEmailType>())
        {
            foreach (var language in Enum.GetValues<Language>())
                data.Add(type, language);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllTypesAndLanguages))]
    public async Task DefaultTemplates_AreValidAndRender(ClubEmailType type, Language language)
    {
        var definition = ClubEmailCatalog.Get(type);
        var template = ClubEmailDefaultTemplates.Get(type, language);

        renderer.Validate(definition, template);
        var rendered = await renderer.Render(template, definition.CreateSample(language) with { Club = ClubEmailCatalog.SampleClub });

        rendered.Subject.Should().NotBeNullOrWhiteSpace();
        rendered.BodyHtml.Should().Contain("Max");
        rendered.BodyHtml.Should().NotContain("{{").And.NotContain("{%");
    }

    [Fact]
    public async Task Render_WelcomeWithoutWebsite_HidesWebsiteSection()
    {
        var definition = ClubEmailCatalog.Get(ClubEmailType.Welcome);
        var variables = definition.CreateSample(Language.English) with { Club = new ClubVariables("TC Test", null, null) };

        var rendered = await renderer.Render(ClubEmailDefaultTemplates.Get(ClubEmailType.Welcome, Language.English), variables);

        rendered.BodyHtml.Should().Contain("TC Test").And.NotContain("class=\"btn\"");
    }

    [Fact]
    public async Task Render_WelcomeWithWebsite_RendersButton()
    {
        var definition = ClubEmailCatalog.Get(ClubEmailType.Welcome);
        var variables = definition.CreateSample(Language.German) with { Club = new ClubVariables("TC Test", "https://tc-test.at/new", null) };

        var rendered = await renderer.Render(ClubEmailDefaultTemplates.Get(ClubEmailType.Welcome, Language.German), variables);

        rendered.BodyHtml.Should().Contain("href=\"https://tc-test.at/new\"").And.Contain("class=\"btn\"");
        rendered.BodyHtml.Should().Contain("Liebe*r Max");
    }

    [Fact]
    public async Task Render_ReplacesVariablesInSubjectAndBody()
    {
        var rendered = await Render("Hello {{ member.first_name }} from {{ club.name }}", "**{{ member.full_name }}** played at {{ club.name }}", "Max_Power");

        rendered.Subject.Should().Be("Hello Max_Power from TC Example");
        rendered.BodyHtml.Should().Contain("<strong>Max_Power Muster</strong> played at TC Example");
    }

    [Fact]
    public async Task Render_MarkdownInValues_IsNotInterpreted()
    {
        var rendered = await Render("Subject", "Hi {{ member.first_name }}", "[click](https://evil.example){.btn} *bold*");

        rendered.BodyHtml.Should().NotContain("<a").And.NotContain("<em>").And.NotContain("class=");
        rendered.BodyHtml.Should().Contain("[click](https://evil.example){.btn} *bold*");
    }

    [Fact]
    public async Task Render_HtmlInValues_IsEncoded()
    {
        var rendered = await Render("Subject", "Hi {{ member.first_name }}", "<script>alert(1)</script>");

        rendered.BodyHtml.Should().NotContain("<script").And.Contain("&lt;script&gt;");
    }

    [Fact]
    public async Task Render_HtmlInTemplate_IsEncoded()
    {
        var rendered = await Render("Subject", "<img src=x onerror=alert(1)> <b>bold</b>");

        rendered.BodyHtml.Should().NotContain("<img").And.NotContain("<b>");
    }

    [Fact]
    public async Task Render_JavascriptLink_IsRemoved()
    {
        var rendered = await Render("Subject", "[click](javascript:alert(1))");

        rendered.BodyHtml.Should().NotContain("javascript:");
    }

    [Fact]
    public async Task Render_ButtonAttribute_AddsBtnClass()
    {
        var rendered = await Render("Subject", "[Open](https://www.example.com){.btn}");

        rendered.BodyHtml.Should().Contain("<a href=\"https://www.example.com\" class=\"btn\">Open</a>");
    }

    [Fact]
    public async Task Render_Conditions_Work()
    {
        var rendered = await Render("Subject", "{% if club.website_url %}website{% else %}no website{% endif %}");

        rendered.BodyHtml.Should().Contain("website").And.NotContain("no website");
    }

    [Fact]
    public async Task Render_MultiLineSubject_IsJoined()
    {
        var rendered = await Render("Hello\n{{ member.first_name }}", "Body");

        rendered.Subject.Should().Be("Hello Max");
    }

    [Fact]
    public void Validate_UnknownRootVariable_Throws()
    {
        var act = () => Validate("Subject", "{{ password }}");

        var exception = act.Should().Throw<PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(ClubEmailTemplateErrorCode.UnknownEmailTemplateVariable));
        exception.ErrorDetails.Should().Equal("password");
    }

    [Fact]
    public void Validate_UnknownNestedVariable_Throws()
    {
        var act = () => Validate("{{ member.email }}", "Body");

        var exception = act.Should().Throw<PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(ClubEmailTemplateErrorCode.UnknownEmailTemplateVariable));
        exception.ErrorDetails.Should().Equal("member.email");
    }

    [Fact]
    public void Validate_VariableOfOtherEmailType_Throws()
    {
        var act = () => Validate("Subject", "{{ badge.name }}", ClubEmailType.Welcome);

        act.Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(ClubEmailTemplateErrorCode.UnknownEmailTemplateVariable));
    }

    [Fact]
    public void Validate_AssignedVariablesAndBuiltIns_AreAllowed()
    {
        var act = () => Validate(
            "Subject",
            "{% assign name = member.first_name | upcase %}{{ name }} {{ member.first_name.size }} {% capture greeting %}Hi{% endcapture %}{{ greeting }}{% if club.website_url == empty %}x{% endif %}");

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_InvalidSyntax_Throws()
    {
        var act = () => Validate("Subject", "{% if member.first_name %}missing endif");

        act.Should().Throw<PreconditionException>()
            .Which.ErrorCode.Should().Be(nameof(ClubEmailTemplateErrorCode.InvalidEmailTemplateSyntax));
    }

    [Fact]
    public void Catalog_ExposesExpectedVariables()
    {
        ClubEmailCatalog.Get(ClubEmailType.BadgeAwarded).VariablePaths.Should().BeEquivalentTo(
            "member.first_name", "member.last_name", "member.full_name",
            "club.name", "club.website_url", "club.reply_to_email",
            "badge.name", "badge.description", "badge.image_url", "badge.is_one_time",
            "trophy_case_url");
    }

    [Fact]
    public void Catalog_AnnouncementExposesTitleBodyAndUrl()
    {
        ClubEmailCatalog.Get(ClubEmailType.Announcement).VariablePaths.Should().BeEquivalentTo(
            "member.first_name", "member.last_name", "member.full_name",
            "club.name", "club.website_url", "club.reply_to_email",
            "announcement.title", "announcement.body", "announcement.url");
    }

    [Fact]
    public async Task Render_AnnouncementBody_IsInsertedAsMarkdownButTheTitleIsEscaped()
    {
        var variables = new AnnouncementEmailVariables(
            new MemberVariables("Max", "Muster"),
            new AnnouncementVariables("*Party*", new MarkdownText("We **celebrate**.\n\n- Food\n- Drinks <script>alert(1)</script>"), "https://www.example.com/news/1"))
        {
            Club = ClubEmailCatalog.SampleClub
        };

        var rendered = await renderer.Render(new ClubEmailTemplateContent("{{ announcement.title }}", "## {{ announcement.title }}\n\n{{ announcement.body }}"), variables);

        rendered.Subject.Should().Be("*Party*");
        rendered.BodyHtml.Should().Contain("*Party*").And.NotContain("<em>Party</em>");
        rendered.BodyHtml.Should().Contain("<strong>celebrate</strong>").And.Contain("<li>Food</li>");
        rendered.BodyHtml.Should().NotContain("<script");
    }

    [Fact]
    public void RenderMarkdown_OpensLinksInANewTabAndRemovesImagesAndHtml()
    {
        var html = renderer.RenderMarkdown("**Bold** [Link](https://example.com) ![Image](https://example.com/a.png) <b>html</b> [Bad](javascript:alert(1))");

        html.Should().Contain("<strong>Bold</strong>");
        html.Should().Contain("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">Link</a>");
        html.Should().NotContain("<img").And.NotContain("<b>").And.NotContain("javascript:");
    }

    [Fact]
    public void ToPlainText_RemovesFormattingAndLineBreaks()
    {
        var text = renderer.ToPlainText("## Title\n\nWe **celebrate** on\nSaturday.\n\n- [Link](https://example.com)");

        text.Should().Be("Title We celebrate on Saturday. Link");
    }

    private void Validate(string subject, string body, ClubEmailType type = ClubEmailType.SeasonActivated)
        => renderer.Validate(ClubEmailCatalog.Get(type), new ClubEmailTemplateContent(subject, body));

    private Task<RenderedClubEmail> Render(string subject, string body, string firstName = "Max")
    {
        var variables = new SeasonActivatedEmailVariables(new MemberVariables(firstName, "Muster"), new SeasonVariables("01.04.2025", "31.10.2025"))
        {
            Club = ClubEmailCatalog.SampleClub
        };
        return renderer.Render(new ClubEmailTemplateContent(subject, body), variables);
    }
}
