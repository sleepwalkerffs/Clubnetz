using Bookennis.Api.Infrastructure.Swagger;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

public class OperationNameFilterTests
{
    [Theory]
    [InlineData("GetCourts", "Get courts")]
    [InlineData("GetClubAnnouncements", "Get club announcements")]
    [InlineData("BookCourt", "Book court")]
    [InlineData("AwardOneTimeBadge", "Award one time badge")]
    [InlineData("Get", "Get")]
    public void Humanize_SplitsTheActionNameIntoWords(string actionName, string expected)
        => OperationNameFilter.Humanize(actionName).Should().Be(expected);
}
