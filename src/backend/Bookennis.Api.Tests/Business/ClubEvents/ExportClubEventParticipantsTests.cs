using Bookennis.Api.Business.ClubEvents;
using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class ExportClubEventParticipantsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ExportClubEventParticipants_ReturnsWorkbookWithRegistrationsAndTotals()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));

        var result = await SendAsync(new ExportClubEventParticipants(TestDataSeed.ClubId, TestDataSeed.AdminId, clubEvent.Id));

        result.FileName.Should().Be($"Work effort {ClubEventSeed.EventDate:yyyy-MM-dd}.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(result.Data));
        workbook.Worksheets.Should().HaveCount(2);

        var sheet = workbook.Worksheets.First();
        sheet.Cell(1, 3).GetString().Should().Be("Staying for food?");
        sheet.Cell(2, 5).GetString().Should().Be("Schnitzel");

        // Registrations are ordered by registration time: Member1 (user) first
        sheet.Cell(3, 1).GetString().Should().Be("us er");
        sheet.Cell(3, 2).GetValue<int>().Should().Be(3);
        sheet.Cell(3, 5).GetValue<int>().Should().Be(2);
        sheet.Cell(3, 7).GetString().Should().Be("Bringing a cake");
        sheet.Cell(4, 1).GetString().Should().Be("ad min");

        // Totals row
        sheet.Cell(5, 2).GetValue<int>().Should().Be(4);
        sheet.Cell(5, 5).GetValue<int>().Should().Be(2);
        sheet.Cell(5, 6).GetValue<int>().Should().Be(1);
    }
}
