using Bookennis.Api.Business.Demo;
using Bookennis.Api.Config;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.Email;
using Fusonic.Extensions.Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bookennis.Api.Tests.Business.Demo;

public class DemoModeTests
{
    private static AppSettings Settings(bool demo) => new() { Demo = new DemoSettings { Enabled = demo } };

    [Fact]
    public async Task GetDemoInfo_OutsideTheDemo_SaysNothingButThat()
    {
        var result = await new GetDemoInfo.Handler(Settings(false)).Handle(new GetDemoInfo(), CancellationToken.None);

        result.IsEnabled.Should().BeFalse();
        result.Password.Should().BeNull();
        result.Clubs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDemoInfo_InTheDemo_ListsTheAccountsOfBothClubs()
    {
        var result = await new GetDemoInfo.Handler(Settings(true)).Handle(new GetDemoInfo(), CancellationToken.None);

        result.IsEnabled.Should().BeTrue();
        result.Password.Should().Be(DemoAccounts.Password);
        result.Clubs.Select(c => c.Region).Should().Equal("eu", "us");
        result.Clubs.Should().OnlyContain(c => c.Accounts.Any(a => a.Persona == nameof(DemoPersona.Admin)) && c.Accounts.Any(a => a.Persona == nameof(DemoPersona.TwoClubs)));
        result.Clubs.SelectMany(c => c.Accounts).Should().OnlyContain(a => DemoAccounts.IsDemoEmail(a.Email));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DisabledInDemo_BlocksTheActionOnlyInTheDemo(bool demo, bool expectRun)
    {
        var services = new ServiceCollection().AddSingleton(Options.Create(Settings(demo))).BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var context = new ActionExecutingContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), new object());
        var ran = false;

        var act = () => new DisabledInDemoAttribute().OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        if (expectRun)
            await act.Should().NotThrowAsync();
        else
            (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(DemoErrorCode.DisabledInDemo));

        ran.Should().Be(expectRun);
    }

    [Theory]
    [InlineData("Jürgen", "Müller", "juergen.mueller@demo.clubnetz.app")]
    [InlineData("Zoë", "O'Brien", "zoe.obrien@demo.clubnetz.app")]
    public void EmailFor_GivesPlainAddressesOfTheDemoDomain(string first, string last, string expected)
        => DemoAccounts.EmailFor(first, last).Should().Be(expected);

    [Fact]
    public void ResetSchedule_IsTheConfiguredTime()
    {
        DemoResetJob.Schedule(new DemoSettings { ResetTime = "03:30" }).Should().Be("30 3 * * *");
        DemoResetJob.Schedule(new DemoSettings { ResetTime = "nonsense" }).Should().Be("0 4 * * *");
    }

    [Theory]
    [InlineData(true, "stranger@example.com", false)]
    [InlineData(true, "lukas.steiner@demo.clubnetz.app", true)]
    [InlineData(false, "stranger@example.com", true)]
    public async Task DemoEmailGuard_InTheDemo_OnlyMailsDemoAddresses(bool demo, string recipient, bool expectSent)
    {
        var inner = Substitute.For<IRequestHandler<SendEmail, Unit>>();
        var guard = new DemoEmailGuard(inner, Settings(demo), NullLogger<DemoEmailGuard>.Instance);
        var email = new SendEmail(recipient, "Someone", System.Globalization.CultureInfo.InvariantCulture, new object(), BccRecipient: "copy@example.com");

        await guard.Handle(email, CancellationToken.None);

        if (!expectSent)
            await inner.DidNotReceiveWithAnyArgs().Handle(default!, default);
        else if (demo)
            await inner.Received(1).Handle(Arg.Is<SendEmail>(e => e.Recipient == recipient && e.BccRecipient == null), Arg.Any<CancellationToken>());
        else
            await inner.Received(1).Handle(email, Arg.Any<CancellationToken>());
    }
}
