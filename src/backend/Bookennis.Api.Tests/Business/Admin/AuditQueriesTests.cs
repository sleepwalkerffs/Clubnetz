using Bookennis.Api.Business.Admin.Audit;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Shared.Controller.Admin.Audit;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class AuditQueriesTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAuditEvents_PagesNewestFirst_WithoutGapsOrDuplicates()
    {
        for (var i = 0; i < 7; i++)
            await Write(new AuditEvent { Action = $"A{i}", Kind = AuditEventKind.Read, OccurredAt = Now.AddMinutes(i) });
        // Two entries in the same moment: the id decides
        await Write(new AuditEvent { Action = "Same1", Kind = AuditEventKind.Read, OccurredAt = Now.AddMinutes(3) });

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await SendAsync(new GetAuditEvents(new AuditEventFilter { PageSize = 3, Cursor = cursor, Kinds = [AuditEventKind.Read] }));
            seen.AddRange(page.Items.Select(i => i.Action));
            cursor = page.NextCursor;
        } while (cursor is not null);

        seen.Should().Equal("A6", "A5", "A4", "Same1", "A3", "A2", "A1", "A0");
    }

    [Fact]
    public async Task GetAuditEvents_Filters()
    {
        var correlation = Guid.NewGuid();
        await Write(new AuditEvent { Action = "Login", Kind = AuditEventKind.Security, Outcome = AuditOutcome.Failed, SubjectUserId = 5, IpAddress = "10.1.2.3", ErrorCode = "InvalidCredentials" });
        await Write(new AuditEvent { Action = AuditChangeCollector.Modified, Kind = AuditEventKind.DataChange, EntityType = "Court", EntityId = "7", ClubId = 1, ActorUserId = 5, CorrelationId = correlation });
        await Write(new AuditEvent { Action = "UpdateCourt", Kind = AuditEventKind.Command, ClubId = 1, ActorUserId = 6, ImpersonatorUserId = 5, CorrelationId = correlation });
        await Write(new AuditEvent { Action = "GetMembers", Kind = AuditEventKind.Read, ClubId = 2, ApiKeyId = 3, OccurredAt = Now.AddDays(-40) });

        (await Actions(new AuditEventFilter { UserId = 5 })).Should().BeEquivalentTo("Login", AuditChangeCollector.Modified, "UpdateCourt");
        (await Actions(new AuditEventFilter { Kinds = [AuditEventKind.Security, AuditEventKind.Read] })).Should().BeEquivalentTo("Login", "GetMembers");
        (await Actions(new AuditEventFilter { Outcomes = [AuditOutcome.Failed] })).Should().Equal("Login");
        (await Actions(new AuditEventFilter { ClubId = 1 })).Should().BeEquivalentTo(AuditChangeCollector.Modified, "UpdateCourt");
        (await Actions(new AuditEventFilter { EntityType = "Court", EntityId = "7" })).Should().Equal(AuditChangeCollector.Modified);
        (await Actions(new AuditEventFilter { Action = "UpdateCourt" })).Should().Equal("UpdateCourt");
        (await Actions(new AuditEventFilter { ImpersonatedOnly = true })).Should().Equal("UpdateCourt");
        (await Actions(new AuditEventFilter { ApiKeyOnly = true })).Should().Equal("GetMembers");
        (await Actions(new AuditEventFilter { CorrelationId = correlation })).Should().BeEquivalentTo(AuditChangeCollector.Modified, "UpdateCourt");
        (await Actions(new AuditEventFilter { From = Now.AddDays(-7) })).Should().NotContain("GetMembers");
        (await Actions(new AuditEventFilter { To = Now.AddDays(-7) })).Should().Equal("GetMembers");
    }

    [Theory]
    [InlineData("10.1.2", "Login")]
    [InlineData("invalidcred", "Login")]
    [InlineData("court", "Modified")]
    [InlineData("100%_", null)]
    public async Task GetAuditEvents_SearchesTheTextColumns(string search, string? expected)
    {
        await Write(new AuditEvent { Action = "Login", Kind = AuditEventKind.Security, IpAddress = "10.1.2.3", ErrorCode = "InvalidCredentials" });
        await Write(new AuditEvent { Action = AuditChangeCollector.Modified, Kind = AuditEventKind.Security, EntityType = "Court", EntityId = "7" });

        var actions = await Actions(new AuditEventFilter { Search = search, Kinds = [AuditEventKind.Security] });

        if (expected is null)
            actions.Should().BeEmpty("% and _ are no wildcards");
        else
            actions.Should().Equal(expected);
    }

    [Fact]
    public async Task GetAuditEvent_ReturnsTheChangesAndTheRestOfTheRequest_WithClubNames()
    {
        var (clubId, clubName) = Query(ctx => (ctx.TestData().Club.Id, ctx.TestData().Club.Name));
        var correlation = Guid.NewGuid();
        await Write(new AuditEvent { Action = "UpdateCourt", Kind = AuditEventKind.Command, CorrelationId = correlation, ClubId = clubId });
        await Write(new AuditEvent
        {
            Action = AuditChangeCollector.Modified, Kind = AuditEventKind.DataChange, CorrelationId = correlation, ClubId = clubId, EntityType = "Court",
            Changes = """{"Name": {"old": "Court 1", "new": "Center"}, "SortOrder": {"new": 3}, "Inactive": {"old": null}}"""
        });
        var id = await QueryAsync(ctx => Task.FromResult(ctx.AuditEvents.Single(e => e.EntityType == "Court").Id));

        var detail = await SendAsync(new GetAuditEvent(id));

        detail.Event.ClubName.Should().Be(clubName);
        detail.Changes.Should().Equal(
            new AuditFieldChange("Inactive", null, null, true, false),
            new AuditFieldChange("Name", "Court 1", "Center", true, true),
            new AuditFieldChange("SortOrder", null, "3", false, true));
        detail.Related.Select(r => r.Action).Should().Equal("UpdateCourt");
    }

    [Fact]
    public async Task GetAuditFilterOptions_ListsTheEntityTypesActionsAndClubs()
    {
        var options = await SendAsync(new GetAuditFilterOptions());

        options.EntityTypes.Should().Contain(["Booking", "Court", "User"]).And.NotContain(nameof(AuditEvent)).And.NotContain("EntityMetadata");
        options.Actions.Should().StartWith([AuditChangeCollector.Created, AuditChangeCollector.Modified, AuditChangeCollector.Deleted, AccessDeniedAudit.Action]);
        options.Actions.Should().Contain(["LoginUser", "ImpersonateUser", "ExportAccounting"]).And.NotContain(["GetCourts", "GetAuditEvents", "AuthenticateClubApiKey"]);
        options.Clubs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAuditEventCounts_CountsByResult_InTheViewersTime_WithEmptyBuckets()
    {
        // 23:30 UTC is already the next day in Vienna (UTC+1)
        await Write(new AuditEvent { Action = "X", Kind = AuditEventKind.Read, OccurredAt = new DateTime(2026, 11, 2, 23, 30, 0, DateTimeKind.Utc) });
        await Write(new AuditEvent { Action = "X", Kind = AuditEventKind.Read, Outcome = AuditOutcome.Failed, OccurredAt = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc) });
        await Write(new AuditEvent { Action = "X", Kind = AuditEventKind.Read, Outcome = AuditOutcome.Denied, OccurredAt = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc) });

        var counts = await SendAsync(new GetAuditEventCounts(new AuditEventFilter
        {
            Kinds = [AuditEventKind.Read],
            From = new DateTime(2026, 11, 1, 23, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 11, 6, 23, 0, 0, DateTimeKind.Utc)
        }, 60));

        counts.BucketSize.Should().Be(AuditBucketSize.Day);
        counts.Buckets.Select(b => b.Start.Day).Should().Equal(2, 3, 4, 5, 6, 7);
        counts.Buckets.Single(b => b.Start.Day == 3).Should().Be(new AuditCountBucket(new DateTime(2026, 11, 3), 1, 1, 0));
        counts.Buckets.Single(b => b.Start.Day == 5).Denied.Should().Be(1);
        counts.Buckets.Single(b => b.Start.Day == 4).Total.Should().Be(0);
    }

    [Theory]
    [InlineData(1, AuditBucketSize.FiveMinutes)]
    [InlineData(24, AuditBucketSize.Hour)]
    [InlineData(24 * 30, AuditBucketSize.Day)]
    [InlineData(24 * 365, AuditBucketSize.Month)]
    public async Task GetAuditEventCounts_ChoosesTheBucketSizeByTheSpan(int hours, AuditBucketSize expected)
    {
        var counts = await SendAsync(new GetAuditEventCounts(new AuditEventFilter { From = Now.AddHours(-hours), To = Now }, 0));

        counts.BucketSize.Should().Be(expected);
        counts.Buckets.Count.Should().BeLessThanOrEqualTo(GetAuditEventCounts.MaxBuckets);
    }

    [Fact]
    public async Task ExportAuditEvents_WritesCsv_DefusesFormulas_AndIsLoggedItself()
    {
        await Write(new AuditEvent { Action = "X", Kind = AuditEventKind.Security, ActorName = "=HYPERLINK(\"evil\")", Summary = "a, \"quoted\" text" });

        var result = await SendAsync(new ExportAuditEvents(new AuditEventFilter { Kinds = [AuditEventKind.Security] }));

        var csv = System.Text.Encoding.UTF8.GetString(result.Data).TrimStart('﻿');
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("OccurredAtUtc,Kind,Action");
        lines[1].Should().Contain("\"'=HYPERLINK(\"\"evil\"\")\"").And.Contain("\"a, \"\"quoted\"\" text\"");
        result.FileName.Should().EndWith(".csv");

        var logged = await QueryAsync(ctx => Task.FromResult(ctx.AuditEvents.Single(e => e.Action == nameof(ExportAuditEvents))));
        logged.Kind.Should().Be(AuditEventKind.Read);
    }

    private async Task<List<string>> Actions(AuditEventFilter filter) => (await SendAsync(new GetAuditEvents(filter))).Items.Select(i => i.Action).ToList();

    private Task Write(AuditEvent e)
    {
        if (e.OccurredAt == default)
            e.OccurredAt = Now;
        return ScopedAsync(() => GetInstance<IAuditWriter>().WriteAsync(e));
    }
}
