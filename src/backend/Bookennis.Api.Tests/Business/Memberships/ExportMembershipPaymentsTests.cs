using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;
using OrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;

namespace Bookennis.Api.Tests.Business.Memberships;

public class ExportMembershipPaymentsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ExportMembershipPayments_WritesOneRowPerMemberAndOnePerOrder()
    {
        var seeded = await QueryAsync(ctx => MembershipPaymentSeed.SeasonWithPayments(ctx));

        var result = await SendAsync(new ExportMembershipPayments(TestDataSeed.ClubId, seeded.Family.SeasonId));

        result.FileName.Should().Be($"Mitgliedsbeiträge {MembershipSeed.Today.AddDays(-30).Year}.xlsx");

        using var workbook = new XLWorkbook(new MemoryStream(result.Data));
        var members = workbook.Worksheet("Mitglieder");
        members.Cell(1, 1).GetString().Should().Be("Nachname");
        members.RowsUsed().Should().HaveCount(8, "a header and the seven members");

        var rows = members.RowsUsed().Skip(1).ToList();
        var parent = rows.Single(r => r.Cell(2).GetString() == "us");
        parent.Cell(3).GetString().Should().Be("Freigeschaltet, online bezahlt");
        parent.Cell(4).GetString().Should().Be("Family");
        parent.Cell(5).GetString().Should().Be("Bezahlt");
        parent.Cell(6).GetDouble().Should().Be(215.0);

        // The amount of a family order is only written once, so the column adds up
        var kid = rows.Single(r => r.Cell(2).GetString() == "Kid");
        kid.Cell(4).GetString().Should().Be("Family");
        kid.Cell(6).IsEmpty().Should().BeTrue();
        kid.Cell(9).GetString().Should().Be("us er");
        rows.Where(r => !r.Cell(6).IsEmpty()).Sum(r => r.Cell(6).GetDouble()).Should().Be(215 + 120 + 120);

        rows.Single(r => r.Cell(2).GetString() == "Nobody").Cell(3).GetString().Should().Be("Nicht freigeschaltet");

        var orders = workbook.Worksheet("Zahlungen");
        orders.RowsUsed().Should().HaveCount(4);
        orders.Column(9).CellsUsed().Select(c => c.GetString()).Should().Contain("cash");
    }
}
