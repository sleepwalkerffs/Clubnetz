using System.Drawing;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Fusonic.Extensions.Common.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IEntity = Bookennis.Domain.Base.IEntity;

namespace Bookennis.Api.Tests;

public class TestDataSeed(AppDbContext dbContext)
{
    public const int AdminId = 1000_000;
    public const int UserId = 1000_0001;

    public const int ClubId = 2000_000;

    public const int Court1Id = 3000_000;
    public const int Court2Id = 3000_001;

    public const int Member1Id = 4000_001;
    public const int Member2Id = 4000_002;

    public async Task Seed()
    {
        // Remove migration-seeded data so tests start with a clean slate
        dbContext.Courts.RemoveRange(dbContext.Courts);
        dbContext.PlayModes.RemoveRange(dbContext.PlayModes);
        dbContext.Clubs.RemoveRange(dbContext.Clubs);
        await dbContext.SaveChangesAsync();

        var adminId = await AddAdmin(AdminId);
        var userId = await AddUser(UserId);

        var clubId = await AddClub(ClubId);

        await AddUserToClub(clubId, userId, MemberRole.User, Member1Id);
        await AddUserToClub(clubId, adminId, MemberRole.Admin, Member2Id);

        await AddCort(clubId, "Court 1", "C1", Court1Id);
        await AddCort(clubId, "Court 2", "C2", Court2Id);
    }

    private Task<Court> AddCort(int clubId, string courtName, string courtAlias, int id)
        => Add(new Court(clubId, courtName, courtAlias), id);

    private async Task AddUserToClub(int clubId, int userId, MemberRole memberRole, int id) => await Add(new ClubMember(userId, clubId, [memberRole]), id);

    private async Task<int> AddClub(int id)
        => (await Add(new Club(name: "TestClub",
                               openingHours: new TimeOnlyInterval(new TimeOnly(07, 00), new TimeOnly(22, 00)),
                               primeTimeHours: new TimeOnlyInterval(new TimeOnly(18, 00), new TimeOnly(20, 00)),
                               playModes: [new Club.PlayModeDto([MemberRole.User], Color.Brown, 2, true, "Single", TimeSpan.FromHours(1.5), false, false)]
                      ), id)
           ).Id;

    private async Task<int> AddAdmin(int id)
    {
        var user = await Add(new User("admin@bookennis.com", "admin@bookennis.com", "ad", "min", new DateOnly(2000, 1, 1), Gender.Male, "Admin St 1", "Vienna", "1010", Country.Austria), id);
        await Add(new IdentityUserRole<int>
        {
            UserId = user.Id,
            RoleId = (int)UserRoles.User
        });

        return user.Id;
    }

    private async Task<int> AddUser(int id)
    {
        var admin = await Add(new User("user@bookennis.com", "user@bookennis.com", "us", "er", new DateOnly(2000, 1, 1), Gender.Male, "User St 1", "Graz", "8010", Country.Austria), id);
        await Add(new IdentityUserRole<int>
        {
            UserId = admin.Id,
            RoleId = (int)UserRoles.Administrator
        });

        return admin.Id;
    }

    private Task<T> Add<T>(T entity, int id)
        where T : class, IEntity
    {
        entity.SetId(id);
        return Add(entity);
    }

    private async Task<T> Add<T>(T entity)
        where T : class
    {
        dbContext.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity;
    }

    public class TestDataAccessor(AppDbContext dbContext)
    {
        public User Admin => dbContext.Users.Find(AdminId) ?? throw new EntityNotFoundException(typeof(User));
        public User User => dbContext.Users.Find(UserId) ?? throw new EntityNotFoundException(typeof(User));
        public Club Club => dbContext.Clubs.Include(c => c.PlayModes).Single(c => c.Id == ClubId) ?? throw new EntityNotFoundException(typeof(Club));
        public Court Court1 => dbContext.Courts.Find(Court1Id) ?? throw new EntityNotFoundException(typeof(Court));
        public Court Court2 => dbContext.Courts.Find(Court2Id) ?? throw new EntityNotFoundException(typeof(Court));
        public ClubMember Member1 => dbContext.ClubMembers.Find(Member1Id) ?? throw new EntityNotFoundException(typeof(ClubMember));
        public ClubMember Member2 => dbContext.ClubMembers.Find(Member2Id) ?? throw new EntityNotFoundException(typeof(ClubMember));
    }
}