using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Clubs;

public class ClubEmailSettingsTests
{
    [Fact]
    public void UpdateContactSettings_TrimsAndStoresValues()
    {
        var club = CreateClub();

        club.UpdateContactSettings(" https://tc-test.at ", " office@tc-test.at ");

        club.WebsiteUrl.Should().Be("https://tc-test.at");
        club.ReplyToEmail.Should().Be("office@tc-test.at");
    }

    [Fact]
    public void UpdateContactSettings_EmptyValues_ClearSettings()
    {
        var club = CreateClub();
        club.UpdateContactSettings("https://tc-test.at", "office@tc-test.at");

        club.UpdateContactSettings(" ", null);

        club.WebsiteUrl.Should().BeNull();
        club.ReplyToEmail.Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("ftp://tc-test.at", null)]
    [InlineData("tc-test.at", null)]
    [InlineData(null, "no-email")]
    public void UpdateContactSettings_InvalidValues_Throw(string? websiteUrl, string? replyToEmail)
    {
        var club = CreateClub();

        var act = () => club.UpdateContactSettings(websiteUrl, replyToEmail);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClubEmailTemplate_Update_TrimsSubject()
    {
        var template = new ClubEmailTemplate(1, ClubEmailType.Welcome, Language.German, " Hallo ", "Body");

        template.Update(" Willkommen ", "Neuer Text");

        template.Subject.Should().Be("Willkommen");
        template.Body.Should().Be("Neuer Text");
    }

    [Theory]
    [InlineData("", "Body")]
    [InlineData("Subject", " ")]
    public void ClubEmailTemplate_EmptyValues_Throw(string subject, string body)
    {
        var act = () => new ClubEmailTemplate(1, ClubEmailType.Welcome, Language.German, subject, body);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClubEmailTemplate_TooLongSubject_Throws()
    {
        var act = () => new ClubEmailTemplate(1, ClubEmailType.Welcome, Language.German, new string('x', ClubEmailTemplate.SubjectMaxLength + 1), "Body");

        act.Should().Throw<ArgumentException>();
    }

    private static Club CreateClub()
        => new("TC Test", new TimeOnlyInterval(new TimeOnly(8, 0), new TimeOnly(22, 0)), new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)), []);
}
