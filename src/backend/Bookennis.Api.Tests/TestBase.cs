using System.Diagnostics;
using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Tests.TestUtils;
using Fusonic.Extensions.Mediator;
using Fusonic.Extensions.UnitTests.EntityFrameworkCore;

namespace Bookennis.Api.Tests;

public abstract class TestBase(TestFixture fixture) : TestBase<TestFixture>(fixture);

public abstract class TestBase<TFixture> : DatabaseUnitTest<AppDbContext, TFixture>
    where TFixture : TestFixture
{
    private readonly ITenantService tenantService;

    protected TestBase(TFixture fixture) : base(fixture)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        tenantService = GetInstance<ITenantService>();
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        tenantService.SetTenantId(clubId);
    }

    [DebuggerStepThrough]
    protected Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        => ScopedAsync(() => GetInstance<IMediator>().Send(request));

    protected void SetTenantId(int id) => tenantService.SetTenantId(id);
}