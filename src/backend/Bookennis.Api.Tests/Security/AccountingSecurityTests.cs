using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// The accounting over HTTP: the books are for the treasurers and admins of the club, the auditors read them and record their
/// audit, nobody else gets in, a treasurer of club B can't reach club A, and nothing is reachable with an API key.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class AccountingSecurityTests(ApiSecurityFixture app)
{
    private static string AccountingOf(SeededClub club) => $"/api/Clubs/{club.Id}/Accounting";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user, string? body = null)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    /// <summary>What the auditors may read, with the seeded ids of the club.</summary>
    private static string[] Reads(SeededClub club) =>
    [
        "",
        $"fiscal-years/{club.FiscalYearId}/summary",
        $"fiscal-years/{club.FiscalYearId}/income-statement",
        $"fiscal-years/{club.FiscalYearId}/cost-centers",
        $"fiscal-years/{club.FiscalYearId}/money-accounts/{club.MoneyAccountId}/sheet",
        $"fiscal-years/{club.FiscalYearId}/export",
        $"fiscal-years/{club.FiscalYearId}/entries",
        $"entries/{club.AccountingEntryId}",
        "receipts",
        $"receipts/{club.AccountingReceiptId}/file",
    ];

    /// <summary>Everything that changes the books, only for treasurers and admins.</summary>
    private static (HttpMethod Method, string Path, string? Body)[] Writes(SeededClub club) =>
    [
        (HttpMethod.Get, "members", null),
        (HttpMethod.Post, "setup", """{"fiscalYearStartMonth":1,"firstYear":2026}"""),
        (HttpMethod.Post, "fiscal-years", null),
        (HttpMethod.Put, $"fiscal-years/{club.FiscalYearId}/opening-balances", $$"""{"balances":[{"moneyAccountId":{{club.MoneyAccountId}},"amountCents":1}]}"""),
        (HttpMethod.Post, $"fiscal-years/{club.FiscalYearId}/close", null),
        (HttpMethod.Post, $"fiscal-years/{club.FiscalYearId}/reopen", null),
        (HttpMethod.Post, "money-accounts", """{"name":"Stolen","kind":"Cash"}"""),
        (HttpMethod.Put, $"money-accounts/{club.MoneyAccountId}", """{"name":"Stolen","kind":"Cash"}"""),
        (HttpMethod.Delete, $"money-accounts/{club.MoneyAccountId}", null),
        (HttpMethod.Post, "categories", """{"name":"Stolen","kind":"Income"}"""),
        (HttpMethod.Put, $"categories/{club.AccountingCategoryId}", """{"name":"Stolen","kind":"Income"}"""),
        (HttpMethod.Delete, $"categories/{club.AccountingCategoryId}", null),
        (HttpMethod.Post, "cost-centers", """{"name":"Stolen"}"""),
        (HttpMethod.Put, $"cost-centers/{club.CostCenterId}", """{"name":"Stolen"}"""),
        (HttpMethod.Delete, $"cost-centers/{club.CostCenterId}", null),
        (HttpMethod.Post, "entries", $$"""{"date":"{{DateTime.UtcNow:yyyy-MM-dd}}","text":"Stolen","kind":"Transfer","moneyAccountId":{{club.MoneyAccountId}},"toMoneyAccountId":{{club.MoneyAccountId}},"transferAmountCents":1}"""),
        (HttpMethod.Put, $"entries/{club.AccountingEntryId}", $$"""{"date":"{{DateTime.UtcNow:yyyy-MM-dd}}","text":"Stolen","kind":"Booking","moneyAccountId":{{club.MoneyAccountId}},"lines":[{"categoryId":{{club.AccountingCategoryId}},"amountCents":1}]}"""),
        (HttpMethod.Post, $"entries/{club.AccountingEntryId}/cancel", """{"reason":"Stolen"}"""),
        (HttpMethod.Delete, $"receipts/{club.AccountingInboxReceiptId}", null),
    ];

    private static string AuditBody => """{"status":"Audited","comment":"Fine"}""";

    [Fact]
    public async Task ApiKey_NeverReachesTheAccounting()
    {
        var key = await app.NewKey(app.ClubA);

        using var scope = new AssertionScope();
        foreach (var path in Reads(app.ClubA))
        {
            var response = await app.Send(HttpMethod.Get, $"{AccountingOf(app.ClubA)}/{path}", key.Bearer);
            response.Status.Should().Be(HttpStatusCode.Unauthorized, path);
            response.RanAsKey.Should().BeFalse(path);
        }
    }

    [Fact]
    public async Task MembersAndOtherRoles_GetNothing()
    {
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            // The plain member is the payer of the seeded entry: being named in the books gives no right to read them
            foreach (var user in new[] { app.ClubA.Member, app.MemberOfA, app.EnrolledMemberOfA, app.HostOfA, app.SportsDirectorOfA })
            {
                foreach (var path in Reads(app.ClubA))
                    (await Send(HttpMethod.Get, $"{AccountingOf(app.ClubA)}/{path}", user)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} GET {path}");
                foreach (var (method, path, body) in Writes(app.ClubA))
                    (await Send(method, $"{AccountingOf(app.ClubA)}/{path}", user, body)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} {method} {path}");
                (await Send(HttpMethod.Post, $"{AccountingOf(app.ClubA)}/fiscal-years/{app.ClubA.FiscalYearId}/audit", user, AuditBody)).Status.Should().Be(HttpStatusCode.Forbidden, user.Email);
            }
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Auditor_ReadsEverything_ButChangesNothing()
    {
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            foreach (var path in Reads(app.ClubA))
                (await Send(HttpMethod.Get, $"{AccountingOf(app.ClubA)}/{path}", app.AuditorOfA)).Status.Should().Be(HttpStatusCode.OK, path);
            foreach (var (method, path, body) in Writes(app.ClubA))
                (await Send(method, $"{AccountingOf(app.ClubA)}/{path}", app.AuditorOfA, body)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");

            var basics = await Send(HttpMethod.Get, AccountingOf(app.ClubA), app.AuditorOfA);
            basics.Body.Should().Contain("\"canManage\":false").And.Contain("\"canAudit\":true").And.NotContain(ApiSecurityFixture.Canary);

            // The year is open: the audit is refused by the rules, not by the policy
            (await Send(HttpMethod.Post, $"{AccountingOf(app.ClubA)}/fiscal-years/{app.ClubA.FiscalYearId}/audit", app.AuditorOfA, AuditBody)).Status.Should().Be(HttpStatusCode.PreconditionFailed);

            // Being an auditor gives no other rights
            (await Send(HttpMethod.Get, $"/api/Clubs/{app.ClubA.Id}/MembershipPayments?seasonId={app.ClubA.SeasonId}", app.AuditorOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Treasurer_KeepsTheBooks_ButDoesNotAuditThem()
    {
        var basics = await Send(HttpMethod.Get, AccountingOf(app.ClubA), app.TreasurerOfA);
        var entry = await Send(HttpMethod.Get, $"{AccountingOf(app.ClubA)}/entries/{app.ClubA.AccountingEntryId}", app.TreasurerOfA);
        var members = await Send(HttpMethod.Get, $"{AccountingOf(app.ClubA)}/members", app.TreasurerOfA);
        var audit = await Send(HttpMethod.Post, $"{AccountingOf(app.ClubA)}/fiscal-years/{app.ClubA.FiscalYearId}/audit", app.TreasurerOfA, AuditBody);

        using var scope = new AssertionScope();
        basics.Status.Should().Be(HttpStatusCode.OK);
        basics.Body.Should().Contain("\"canManage\":true").And.Contain($"Bank {ApiSecurityFixture.Marker}").And.NotContain(ApiSecurityFixture.Canary);
        entry.Status.Should().Be(HttpStatusCode.OK);
        entry.Body.Should().Contain($"Entry {ApiSecurityFixture.Marker}");
        members.Status.Should().Be(HttpStatusCode.OK);
        members.Body.Should().Contain($"First{ApiSecurityFixture.Marker}").And.NotContain("@security.test").And.NotContain("1990").And.NotContain(ApiSecurityFixture.Canary);
        audit.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TreasurerOfAnotherClub_CannotTouchTheBooks()
    {
        // The admin of club B keeps the books of club B
        var attacker = app.ClubB.Admin;
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            // Through the routes of club A: no member there
            foreach (var path in Reads(app.ClubA))
                (await Send(HttpMethod.Get, $"{AccountingOf(app.ClubA)}/{path}", attacker)).Status.Should().Be(HttpStatusCode.Forbidden, path);
            foreach (var (method, path, body) in Writes(app.ClubA))
                (await Send(method, $"{AccountingOf(app.ClubA)}/{path}", attacker, body)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");

            // Through the routes of club B with ids of club A: not found
            foreach (var path in Reads(app.ClubA).Where(p => p.Contains('/')))
                (await Send(HttpMethod.Get, $"{AccountingOf(app.ClubB)}/{path}", attacker)).Status.Should().Be(HttpStatusCode.NotFound, path);
            foreach (var (method, path, body) in Writes(app.ClubA).Where(w => w.Path.Contains('/')))
                (await Send(method, $"{AccountingOf(app.ClubB)}/{path}", attacker, body)).Status.Should().Be(HttpStatusCode.NotFound, $"{method} {path}");

            // An entry of club B onto the bank account and the category of club A, with a receipt and a payer of club A
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            (await Send(HttpMethod.Post, $"{AccountingOf(app.ClubB)}/entries", attacker,
                    $$"""{"date":"{{date}}","text":"X","kind":"Booking","moneyAccountId":{{app.ClubA.MoneyAccountId}},"lines":[{"categoryId":{{app.ClubB.AccountingCategoryId}},"amountCents":1}]}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Post, $"{AccountingOf(app.ClubB)}/entries", attacker,
                    $$"""{"date":"{{date}}","text":"X","kind":"Booking","moneyAccountId":{{app.ClubB.MoneyAccountId}},"lines":[{"categoryId":{{app.ClubA.AccountingCategoryId}},"amountCents":1}]}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Post, $"{AccountingOf(app.ClubB)}/entries", attacker,
                    $$"""{"date":"{{date}}","text":"X","kind":"Booking","moneyAccountId":{{app.ClubB.MoneyAccountId}},"lines":[{"categoryId":{{app.ClubB.AccountingCategoryId}},"amountCents":1}],"receiptIds":[{{app.ClubA.AccountingInboxReceiptId}}]}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Post, $"{AccountingOf(app.ClubB)}/entries", attacker,
                    $$"""{"date":"{{date}}","text":"X","kind":"Booking","moneyAccountId":{{app.ClubB.MoneyAccountId}},"lines":[{"categoryId":{{app.ClubB.AccountingCategoryId}},"amountCents":1}],"counterpartyMemberId":{{app.MemberOfA.MemberId}}}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);

            // What club B lists: nothing of club A (not checked: the members of club B, the admin of club A who plays in B is one of them, under the name of club A)
            foreach (var path in new[] { "", "receipts", $"fiscal-years/{app.ClubB.FiscalYearId}/entries" })
                (await Send(HttpMethod.Get, $"{AccountingOf(app.ClubB)}/{path}", attacker)).Body.Should().NotContain(ApiSecurityFixture.Marker, path);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }
}
