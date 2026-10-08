using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Tests.TestUtils;

public static class DbContextExtensions
{
    public static void Clear<T>(this DbSet<T> dbSet)
        where T : class
        => dbSet.RemoveRange(dbSet.IgnoreQueryFilters().ToList());

    public static TestDataSeed.TestDataAccessor TestData(this AppDbContext dbContext) => new(dbContext);

    /// <summary>
    /// Removes clubs and courts added by migrations (non-test data) so that
    /// handlers relying on tenant-scoped SingleRequiredAsync work correctly.
    /// </summary>
    public static async Task RemoveMigrationSeedData(this AppDbContext dbContext)
    {
        var nonTestCourts = dbContext.Courts.IgnoreQueryFilters().Where(c => c.ClubId != TestDataSeed.ClubId).ToList();
        dbContext.Courts.RemoveRange(nonTestCourts);

        var nonTestPlayModes = dbContext.PlayModes.IgnoreQueryFilters().Where(p => p.ClubId != TestDataSeed.ClubId).ToList();
        dbContext.PlayModes.RemoveRange(nonTestPlayModes);

        var nonTestClubs = dbContext.Clubs.IgnoreQueryFilters().Where(c => c.Id != TestDataSeed.ClubId).ToList();
        dbContext.Clubs.RemoveRange(nonTestClubs);

        await dbContext.SaveChangesAsync();
    }
}