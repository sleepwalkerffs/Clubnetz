using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Admin.Audit;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

public record AuditTestCommand : ICommand;

public record AuditTestNestedCommand : ICommand;

[SecurityAudit]
public record AuditTestSecurityCommand(int UserId) : ICommand;

public record AuditTestQuery : IQuery<int>;

/// <summary>Some reads are declared as commands.</summary>
public record GetAuditTestThing : ICommand<int>;

[AuditedRead]
public record AuditTestExport : IQuery<int>;

public class AuditLogTests(TestFixture fixture) : TestBase(fixture)
{
    // ---------- data changes ----------

    [Fact]
    public async Task ChangedRow_IsRecordedWithOldAndNewValues_AndLinkedToItsCommand()
    {
        var (clubId, courtId) = Query(ctx => (ctx.TestData().Club.Id, ctx.TestData().Court1.Id));

        await SendAsync(new AdminUpdateCourt(clubId, courtId, "Audit Renamed", "AR", 99, null));

        var change = await QueryAsync(ctx => ctx.AuditEvents.SingleAsync(e => e.Kind == AuditEventKind.DataChange && e.EntityType == "Court" && e.EntityId == courtId.ToString()));
        change.Action.Should().Be(AuditChangeCollector.Modified);
        change.ClubId.Should().Be(clubId);
        Value(change, "Name", "old").Should().Be("Court 1");
        Value(change, "Name", "new").Should().Be("Audit Renamed");
        change.Changes.Should().NotContain("Metadata", "the created and modified timestamps are noise");
        change.Summary.Should().StartWith("Modified: ").And.Contain("Name");

        var command = await QueryAsync(ctx => ctx.AuditEvents.SingleAsync(e => e.Kind == AuditEventKind.Command && e.Action == nameof(AdminUpdateCourt)));
        command.Outcome.Should().Be(AuditOutcome.Succeeded);
        command.CorrelationId.Should().Be(change.CorrelationId);
        command.DurationMs.Should().NotBeNull();
    }

    [Fact]
    public async Task CreatedAndDeletedRows_AreRecordedWithTheirValues()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        await SendAsync(new AdminAddCourt(clubId, "Audit Court", "AC", 50));
        var courtId = await QueryAsync(ctx => ctx.Courts.Where(c => c.Name == "Audit Court").Select(c => c.Id).SingleAsync());
        await SendAsync(new AdminDeleteCourt(clubId, courtId));

        var events = await QueryAsync(ctx => ctx.AuditEvents.Where(e => e.EntityType == "Court" && e.EntityId == courtId.ToString()).OrderBy(e => e.Id).ToListAsync());
        events.Select(e => e.Action).Should().Equal(AuditChangeCollector.Created, AuditChangeCollector.Deleted);
        Value(events[0], "Name", "new").Should().Be("Audit Court");
        Value(events[1], "Name", "old").Should().Be("Audit Court");
    }

    [Fact]
    public async Task Secrets_AreRedacted()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var user = await ctx.Users.OrderBy(u => u.Id).FirstAsync();
            user.PasswordHash = "secret-hash-value";
            await ctx.SaveChangesAsync();
            return user.Id;
        });

        var change = await QueryAsync(ctx => ctx.AuditEvents.SingleAsync(e => e.EntityType == "User" && e.EntityId == userId.ToString()));
        change.Changes.Should().Contain("PasswordHash").And.Contain("***").And.NotContain("secret-hash-value");
        change.SubjectUserId.Should().Be(userId);
    }

    // ---------- commands ----------

    [Fact]
    public async Task FailedSecurityCommand_IsRecordedWithErrorCodeAndSubject()
    {
        var act = () => SendAsync(new ImpersonateUser(987654));
        await act.Should().ThrowAsync<PreconditionException>();

        var e = await QueryAsync(ctx => ctx.AuditEvents.SingleAsync(x => x.Action == nameof(ImpersonateUser)));
        e.Kind.Should().Be(AuditEventKind.Security);
        e.Outcome.Should().Be(AuditOutcome.Failed);
        e.ErrorCode.Should().Contain("UserNotFound");
        e.SubjectUserId.Should().Be(987654, "the user id of the request names who it is about");
    }

    [Fact]
    public async Task DeniedCommand_IsRecordedAsDenied_AndMarked()
    {
        var exception = new AccessDeniedException("no");
        await Run(new AuditTestCommand(), Context(), _ => Task.FromException<Unit>(exception));

        var e = await Single(nameof(AuditTestCommand));
        e.Outcome.Should().Be(AuditOutcome.Denied);
        exception.Data.Contains(AccessDeniedAudit.LoggedKey).Should().BeTrue("ExceptionFilter must not log it a second time");
    }

    [Fact]
    public async Task NestedCommand_IsPartOfItsParent()
    {
        var context = Context();
        await Run(new AuditTestCommand(), context, async _ =>
        {
            await Decorator<AuditTestNestedCommand, Unit>(context, _ => Task.FromResult(default(Unit))).Handle(new AuditTestNestedCommand(), default);
            return default;
        });

        (await Count(nameof(AuditTestCommand))).Should().Be(1);
        (await Count(nameof(AuditTestNestedCommand))).Should().Be(0);
    }

    [Fact]
    public async Task Queries_AreNotRecorded_ExceptSensitiveReads()
    {
        var context = Context();
        await ScopedAsync(async () =>
        {
            await Decorator<AuditTestQuery, int>(context, _ => Task.FromResult(1)).Handle(new AuditTestQuery(), default);
            await Decorator<AuditTestExport, int>(context, _ => Task.FromResult(1)).Handle(new AuditTestExport(), default);
            await Decorator<GetAuditTestThing, int>(context, _ => Task.FromResult(1)).Handle(new GetAuditTestThing(), default);
        });

        (await Count(nameof(AuditTestQuery))).Should().Be(0);
        (await Count(nameof(GetAuditTestThing))).Should().Be(0);
        (await Single(nameof(AuditTestExport))).Kind.Should().Be(AuditEventKind.Read);
    }

    [Fact]
    public async Task BackgroundCommand_ThatChangesNothing_IsLeftOut_ButItsFailureIsNot()
    {
        await Run(new AuditTestCommand(), Context(system: "TestJob"), _ => Task.FromResult(default(Unit)));
        (await Count(nameof(AuditTestCommand))).Should().Be(0);

        await Run(new AuditTestCommand(), Context(system: "TestJob"), _ => Task.FromException<Unit>(new InvalidOperationException()));
        var e = await Single(nameof(AuditTestCommand));
        e.Outcome.Should().Be(AuditOutcome.Failed);
        e.ErrorCode.Should().Be(nameof(InvalidOperationException));
        e.System.Should().Be("TestJob");
    }

    [Fact]
    public async Task SecurityCommand_TakesTheSubjectFromTheRequest_AndTheActorFromTheSession()
    {
        await Run(new AuditTestSecurityCommand(42), Context(), _ => Task.FromResult(default(Unit)));

        var e = await Single(nameof(AuditTestSecurityCommand));
        e.Kind.Should().Be(AuditEventKind.Security);
        e.SubjectUserId.Should().Be(42);
        e.ActorUserId.Should().Be(7);
        e.ImpersonatorUserId.Should().Be(1);
        e.IpAddress.Should().Be("10.0.0.1");
    }

    // ---------- writer ----------

    [Fact]
    public async Task PseudonymizeUser_RemovesNameEmailAndUserValues_OfThatUserOnly()
    {
        await Write(new AuditEvent { ActorUserId = 500, ActorName = "Real Name", ActorEmail = "real@test.com", Action = "A" });
        await Write(new AuditEvent { SubjectUserId = 500, SubjectName = "Real Name", ImpersonatorUserId = 500, ImpersonatorName = "Real Name", Action = "B" });
        await Write(new AuditEvent { EntityType = "User", EntityId = "500", Changes = """{"Street":{"old":"Secret Street 1"}}""", Action = "C" });
        await Write(new AuditEvent { ActorUserId = 501, ActorName = "Other Name", ActorEmail = "other@test.com", Action = "D" });

        await ScopedAsync(() => GetInstance<IAuditWriter>().PseudonymizeUserAsync(500));

        var events = await QueryAsync(ctx => ctx.AuditEvents.OrderBy(e => e.Action).ToListAsync());
        events[0].ActorName.Should().Be(AuditWriter.DeletedUserName);
        events[0].ActorEmail.Should().BeNull();
        events[1].SubjectName.Should().Be(AuditWriter.DeletedUserName);
        events[1].ImpersonatorName.Should().Be(AuditWriter.DeletedUserName);
        events[2].Changes.Should().BeNull();
        events[3].ActorName.Should().Be("Other Name");
        events[3].ActorEmail.Should().Be("other@test.com");
    }

    [Fact]
    public async Task DeleteExpired_KeepsSecurityEventsTwoYears_AndTheRestThreeYears()
    {
        var now = DateTime.UtcNow;
        await Write(new AuditEvent { Kind = AuditEventKind.Command, OccurredAt = now.AddYears(-4), Action = "OldCommand" });
        await Write(new AuditEvent { Kind = AuditEventKind.DataChange, OccurredAt = now.AddYears(-2).AddDays(-30), Action = "KeptChange" });
        await Write(new AuditEvent { Kind = AuditEventKind.Security, OccurredAt = now.AddYears(-2).AddDays(-30), Action = "OldLogin" });
        await Write(new AuditEvent { Kind = AuditEventKind.Security, OccurredAt = now.AddYears(-1), Action = "KeptLogin" });

        var deleted = await ScopedAsync(() => GetInstance<IAuditWriter>().DeleteExpiredAsync(now));

        deleted.Should().Be(2);
        var left = await QueryAsync(ctx => ctx.AuditEvents.Select(e => e.Action).OrderBy(a => a).ToListAsync());
        left.Should().Equal("KeptChange", "KeptLogin");
    }

    [Fact]
    public async Task AuditEvents_CannotBeChangedOrDeleted_OutsideOfTheWriter()
    {
        await Write(new AuditEvent { Action = "Fixed" });

        var update = () => QueryAsync(ctx => ctx.AuditEvents.ExecuteUpdateAsync(s => s.SetProperty(e => e.Action, "Changed")));
        (await update.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");

        var delete = () => QueryAsync(ctx => ctx.AuditEvents.ExecuteDeleteAsync());
        (await delete.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");
    }

    // ---------- helpers ----------

    private static IAuditContext Context(string? system = null)
    {
        var context = Substitute.For<IAuditContext>();
        context.CorrelationId.Returns(Guid.NewGuid());
        context.GetActor().Returns(new AuditActor(7, "Actor", "actor@test.com", 1, "Admin", null, system, system is null ? "10.0.0.1" : null, null, null));
        context.TakeUsersToPseudonymize().Returns([]);
        return context;
    }

    private AuditRequestHandlerDecorator<TRequest, TResponse> Decorator<TRequest, TResponse>(IAuditContext context, Func<TRequest, Task<TResponse>> handle)
        where TRequest : IRequest<TResponse>
    {
        var inner = Substitute.For<IRequestHandler<TRequest, TResponse>>();
        inner.Handle(Arg.Any<TRequest>(), Arg.Any<CancellationToken>()).Returns(call => handle(call.Arg<TRequest>()));
        return new AuditRequestHandlerDecorator<TRequest, TResponse>(inner, context, GetInstance<IAuditWriter>());
    }

    /// <summary>Runs the request through the decorator in a scope. A thrown exception is swallowed: the tests look at what was logged.</summary>
    private Task Run<TRequest>(TRequest request, IAuditContext context, Func<TRequest, Task<Unit>> handle)
        where TRequest : IRequest<Unit>
        => ScopedAsync(async () => await Record.ExceptionAsync(() => Decorator(context, handle).Handle(request, default)));

    private Task Write(AuditEvent e)
    {
        if (e.OccurredAt == default)
            e.OccurredAt = DateTime.UtcNow;
        return ScopedAsync(() => GetInstance<IAuditWriter>().WriteAsync(e));
    }

    private static string? Value(AuditEvent e, string property, string side)
        => System.Text.Json.JsonDocument.Parse(e.Changes!).RootElement.GetProperty(property).GetProperty(side).GetString();

    private Task<AuditEvent> Single(string action) => QueryAsync(ctx => ctx.AuditEvents.SingleAsync(e => e.Action == action));

    private Task<int> Count(string action) => QueryAsync(ctx => ctx.AuditEvents.CountAsync(e => e.Action == action));
}
