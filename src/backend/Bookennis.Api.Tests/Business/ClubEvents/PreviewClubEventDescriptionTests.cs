using Bookennis.Api.Business.ClubEvents;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class PreviewClubEventDescriptionTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task PreviewClubEventDescription_RendersSanitizedMarkdown()
    {
        var result = await SendAsync(new PreviewClubEventDescription("**Bold** <script>alert(1)</script>\n\n[Link](https://example.com) ![Image](https://example.com/a.png) [Bad](javascript:alert(1))"));

        result.Html.Should().Contain("<strong>Bold</strong>");
        result.Html.Should().Contain("href=\"https://example.com\"").And.Contain("target=\"_blank\"").And.Contain("rel=\"noopener noreferrer\"");
        result.Html.Should().NotContain("<script").And.NotContain("<img").And.NotContain("javascript:");
    }

    [Fact]
    public async Task PreviewClubEventDescription_TextTooLong_ThrowsPreconditionException()
    {
        var act = () => SendAsync(new PreviewClubEventDescription(new string('a', ClubEvent.MaxDescriptionLength + 1)));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubEvent.ErrorCode.ClubEventTextTooLong));
    }
}
