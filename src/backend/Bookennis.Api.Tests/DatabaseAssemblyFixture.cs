using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Fusonic.Extensions.UnitTests;
using Fusonic.Extensions.UnitTests.EntityFrameworkCore.Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

[assembly: AssemblyFixture(typeof(Bookennis.Api.Tests.DatabaseAssemblyFixture))]

namespace Bookennis.Api.Tests;

public class DatabaseAssemblyFixture : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        var configuration = TestConfigurationHelper.GetDefaultConfiguration(
            Directory.GetCurrentDirectory(),
            typeof(DatabaseAssemblyFixture).Assembly);

        var connectionString = configuration.GetConnectionString("Npgsql")
            ?? throw new PreconditionException("No connection string configured");

        await PostgreSqlUtil.CreateTestDbTemplate<AppDbContext>(
            connectionString,
            o => new AppDbContext(o, null, null, null),
            seed: c => new TestDataSeed(c).Seed());
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
