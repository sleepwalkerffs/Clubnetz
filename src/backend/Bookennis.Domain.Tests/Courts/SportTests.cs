using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Courts;

/// <summary>A value that is not a sport (sent by a script) is invalid input, not a server error.</summary>
public class SportTests
{
    private const Sport Unknown = (Sport)5;

    [Fact]
    public void Normalize_UnknownSport_IsInvalidInput()
    {
        var act = () => Sports.Normalize([Sport.Padel, Unknown]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be("InvalidInput");
    }

    [Fact]
    public void Normalize_DuplicatesAndOrder_AreCleanedUp()
        => Sports.Normalize([Sport.Padel, Sport.Tennis, Sport.Padel]).Should().Equal(Sport.Tennis, Sport.Padel);

    [Fact]
    public void Enrollment_UnknownSport_IsInvalidInput()
    {
        var act = () => new MemberSeason(1, 1, sports: [Unknown]);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be("InvalidInput");
    }

    [Fact]
    public void BadgeTier_UnknownSport_IsInvalidInput()
    {
        var act = () => new BadgeTier(1, 1, 1, "Gold", "", 10, 0, Unknown);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be("InvalidInput");
    }
}
