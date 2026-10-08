using Bookennis.Api.Business.SubscriptionPlans;
using Bookennis.Shared.Controller.SubscriptionPlans;
using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.SubscriptionPlans;

public class ExportSubscriptionPlanTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ExportSubscriptionPlan_ReturnsColoredWorkbook()
    {
        var planId = await QueryAsync(async ctx =>
            (await SubscriptionPlanSeed.SeedPlan(ctx, TestDataSeed.UserId, excludedWeeks: [SubscriptionPlanSeed.ChristmasWeek], withSchedule: true)).Id);
        var plan = await SendAsync(new GetSubscriptionPlan(TestDataSeed.ClubId, planId));

        var result = await SendAsync(new ExportSubscriptionPlan(TestDataSeed.ClubId, planId));

        result.FileName.Should().Be("Winter 2627.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(result.Data));
        workbook.Worksheets.Should().HaveCount(3);

        var schedule = workbook.Worksheets.First();
        schedule.Cell(2, 1).GetValue<int>().Should().Be(40);

        var firstPlayer = plan.Participants.Single(p => p.Id == plan.Weeks[0].ParticipantIds[0]);
        schedule.Cell(2, 3).GetString().Should().Be(firstPlayer.Name);
        schedule.Cell(2, 3).Style.Fill.BackgroundColor.Color.Should().Be(XLColor.FromHtml(SubscriptionPlanColors.Get(firstPlayer.ColorIndex).Background).Color);

        var christmasRow = 2 + plan.Weeks.FindIndex(w => w.IsExcluded);
        schedule.Cell(christmasRow, 3).GetString().Should().NotBeEmpty();
    }
}
