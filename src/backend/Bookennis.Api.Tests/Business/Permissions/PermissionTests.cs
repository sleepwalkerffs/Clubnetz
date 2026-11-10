using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Permissions;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SharedPermission = Bookennis.Shared.Controller.Permissions.Permission;

namespace Bookennis.Api.Tests.Business.Permissions;

public class PermissionTests(TestFixture fixture) : TestBase(fixture)
{
    private static Task<bool> Has(Bookennis.Api.Data.AppDbContext ctx, int memberId, MemberRole[] roles, Permission permission, PermissionResourceType? type = null, int? id = null)
        => PermissionEvaluator.Has(ctx, TestDataSeed.ClubId, memberId, roles, permission, type, id, CancellationToken.None);

    [Fact]
    public void Catalog_HasEveryPermissionOnce_AndTheSharedEnumMirrorsIt()
    {
        PermissionCatalog.All.Select(p => p.Permission).Should().BeEquivalentTo(Enum.GetValues<Permission>());
        Enum.GetNames<Permission>().Should().Equal(Enum.GetNames<SharedPermission>());
        Enum.GetValues<Permission>().Select(p => (int)p).Should().Equal(Enum.GetValues<SharedPermission>().Select(p => (int)p));
    }

    [Theory]
    [InlineData(MemberRole.Maintainer, Permission.MembersView, true)]
    [InlineData(MemberRole.Maintainer, Permission.StatisticsView, true)]
    [InlineData(MemberRole.Maintainer, Permission.EventsManage, true)]
    [InlineData(MemberRole.Maintainer, Permission.CourtBlockingsManage, true)]
    [InlineData(MemberRole.Maintainer, Permission.BookingsManageForAnyMember, false)]
    [InlineData(MemberRole.Maintainer, Permission.PaymentsManage, false)]
    [InlineData(MemberRole.SportsDirector, Permission.OneTimeBadgesManage, true)]
    [InlineData(MemberRole.SportsDirector, Permission.BookingsManageForAnyMember, true)]
    [InlineData(MemberRole.SportsDirector, Permission.MembersView, false)]
    [InlineData(MemberRole.YouthSportsDirector, Permission.CourtBlockingsManage, false)]
    [InlineData(MemberRole.Trainer, Permission.BookingsManageForAnyMember, true)]
    [InlineData(MemberRole.Trainer, Permission.EventsManage, false)]
    [InlineData(MemberRole.Treasurer, Permission.PaymentsManage, true)]
    [InlineData(MemberRole.Treasurer, Permission.AccountingManage, true)]
    [InlineData(MemberRole.Treasurer, Permission.AccountingAudit, false)]
    [InlineData(MemberRole.Auditor, Permission.AccountingRead, true)]
    [InlineData(MemberRole.Auditor, Permission.AccountingAudit, true)]
    [InlineData(MemberRole.Auditor, Permission.AccountingManage, false)]
    [InlineData(MemberRole.ClubhouseHost, Permission.ClubhouseHost, true)]
    [InlineData(MemberRole.ClubhouseHost, Permission.PaymentsManage, false)]
    [InlineData(MemberRole.User, Permission.MembersView, false)]
    public async Task Defaults_EqualTheRolesThePoliciesHadBefore(MemberRole role, Permission permission, bool expected)
    {
        var result = await QueryAsync(ctx => Has(ctx, TestDataSeed.Member1Id, [role], permission));

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Admin_HasEveryPermission_EvenWhenTheMatrixIsEmptied()
    {
        foreach (var role in PermissionCatalog.EditableRoles)
            await SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, role, []));

        var results = await QueryAsync(async ctx =>
        {
            var all = new List<bool>();
            foreach (var permission in Enum.GetValues<Permission>())
                all.Add(await Has(ctx, TestDataSeed.Member2Id, [MemberRole.Admin], permission));
            return all;
        });

        results.Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public async Task UpdateRolePermissions_ChangesOnlyThatRole_AndTakesEffectAtOnce()
    {
        await SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, MemberRole.Trainer, [Permission.EventsManage]));

        var (trainerEvents, trainerBooking, maintainerMembers) = await QueryAsync(async ctx => (
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.Trainer], Permission.EventsManage),
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.Trainer], Permission.BookingsManageForAnyMember),
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.Maintainer], Permission.MembersView)));

        trainerEvents.Should().BeTrue();
        trainerBooking.Should().BeFalse();
        maintainerMembers.Should().BeTrue("the other roles keep their defaults");
    }

    [Fact]
    public async Task UpdateRolePermissions_Admin_IsRejected()
    {
        var act = () => SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, MemberRole.Admin, []));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(UpdateRolePermissions.ErrorCode.PermissionRoleNotEditable));
    }

    [Fact]
    public async Task ResetRolePermissions_RestoresTheDefaultsOfTheRole()
    {
        await SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, MemberRole.Maintainer, []));
        await SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, MemberRole.Treasurer, [Permission.MembersView]));

        await SendAsync(new ResetRolePermissions(TestDataSeed.ClubId, MemberRole.Maintainer));

        var result = await SendAsync(new GetRolePermissions(TestDataSeed.ClubId));
        result.Roles.Single(r => r.Role == Bookennis.Shared.Controller.Shared.MemberRole.Maintainer).Permissions
            .Should().BeEquivalentTo(result.Roles.Single(r => r.Role == Bookennis.Shared.Controller.Shared.MemberRole.Maintainer).DefaultPermissions);
        result.Roles.Single(r => r.Role == Bookennis.Shared.Controller.Shared.MemberRole.Treasurer).Permissions
            .Should().Equal(SharedPermission.MembersView);
    }

    [Fact]
    public async Task ResetAll_RemovesTheStoredMatrix_AndTheDefaultsApplyAgain()
    {
        await SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, MemberRole.Maintainer, []));

        await SendAsync(new ResetRolePermissions(TestDataSeed.ClubId, null));

        (await QueryAsync(ctx => ctx.RolePermissions.CountAsync())).Should().Be(0);
        (await QueryAsync(ctx => Has(ctx, TestDataSeed.Member1Id, [MemberRole.Maintainer], Permission.MembersView))).Should().BeTrue();
    }

    [Fact]
    public async Task AMatrixEmptiedOnPurpose_StaysEmpty_InsteadOfFallingBackToTheDefaults()
    {
        foreach (var role in PermissionCatalog.EditableRoles)
            await SendAsync(new UpdateRolePermissions(TestDataSeed.ClubId, role, []));

        (await QueryAsync(ctx => ctx.RolePermissions.CountAsync())).Should().Be(0);
        (await QueryAsync(ctx => Has(ctx, TestDataSeed.Member1Id, [MemberRole.Treasurer], Permission.AccountingManage))).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingAPlayMode_RemovesItsGrants()
    {
        var playModeId = await QueryAsync(ctx => Task.FromResult(ctx.TestData().Club.PlayModes.First().Id));
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.BookingsManageForAnyMember, PermissionResourceType.PlayMode, playModeId, PermissionEffect.Allow, null));

        await SendAsync(new Bookennis.Api.Business.Clubs.DeletePlayMode(playModeId));

        (await QueryAsync(ctx => ctx.PermissionGrants.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Grant_AllowsAMemberWithoutTheRole()
    {
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.StatisticsView, null, null, PermissionEffect.Allow, "  Press  "));

        var (has, note) = await QueryAsync(async ctx => (
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.User], Permission.StatisticsView),
            (await ctx.PermissionGrants.SingleAsync()).Note));

        has.Should().BeTrue();
        note.Should().Be("Press");
    }

    [Fact]
    public async Task Grant_ForOneCourt_OnlyCoversThatCourt_ButOpensTheDoor()
    {
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court1Id, PermissionEffect.Allow, null));

        var (door, court1, court2) = await QueryAsync(async ctx => (
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.User], Permission.CourtBlockingsManage),
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.User], Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court1Id),
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.User], Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court2Id)));

        door.Should().BeTrue();
        court1.Should().BeTrue();
        court2.Should().BeFalse();
    }

    [Fact]
    public async Task Deny_BeatsTheRole_AndAnAllow()
    {
        await QueryAsync(async ctx =>
        {
            ctx.TestData().Member1.UpdateRoles([MemberRole.Maintainer]);
            await ctx.SaveChangesAsync();
        });
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court2Id, PermissionEffect.Deny, null));
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.MembersView, null, null, PermissionEffect.Deny, null));

        var (court1, court2, members) = await QueryAsync(async ctx => (
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.Maintainer], Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court1Id),
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.Maintainer], Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court2Id),
            await Has(ctx, TestDataSeed.Member1Id, [MemberRole.Maintainer], Permission.MembersView)));

        court1.Should().BeTrue();
        court2.Should().BeFalse("a deny for the court wins over the role");
        members.Should().BeFalse("a general deny wins over the role");
    }

    [Fact]
    public async Task Grant_ForAnAdmin_IsRejected()
    {
        var act = () => SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member2Id, Permission.MembersView, null, null, PermissionEffect.Deny, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreatePermissionGrant.ErrorCode.PermissionGrantForAdmin));
    }

    [Fact]
    public async Task Grant_WithAResourceOfTheWrongKind_IsRejected()
    {
        var act = () => SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.MembersView, PermissionResourceType.Court, TestDataSeed.Court1Id, PermissionEffect.Allow, null));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CreatePermissionGrant.ErrorCode.PermissionGrantResourceInvalid));
    }

    [Fact]
    public async Task Grant_Twice_KeepsOneGrantWithTheLastEffect()
    {
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.StatisticsView, null, null, PermissionEffect.Allow, null));
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.StatisticsView, null, null, PermissionEffect.Deny, null));

        var grants = await SendAsync(new GetPermissionGrants(TestDataSeed.ClubId, TestDataSeed.Member1Id));

        grants.Grants.Should().ContainSingle().Which.Effect.Should().Be(Bookennis.Shared.Controller.Permissions.PermissionEffect.Deny);
    }

    [Fact]
    public async Task GetPermissionGrants_NamesTheCourt_AndDeleteRemovesIt()
    {
        var id = await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court1Id, PermissionEffect.Allow, null));

        var grants = await SendAsync(new GetPermissionGrants(TestDataSeed.ClubId, null));
        grants.Grants.Should().ContainSingle().Which.ResourceName.Should().Be("Court 1");

        await SendAsync(new DeletePermissionGrant(TestDataSeed.ClubId, id));
        (await SendAsync(new GetPermissionGrants(TestDataSeed.ClubId, null))).Grants.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffective_CombinesRolesAndGrants()
    {
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.StatisticsView, null, null, PermissionEffect.Allow, null));

        var effective = await QueryAsync(ctx => PermissionEvaluator.GetEffective(ctx, TestDataSeed.ClubId, TestDataSeed.Member1Id, [MemberRole.Treasurer], CancellationToken.None));

        effective.Should().Contain([Permission.StatisticsView, Permission.PaymentsManage]).And.NotContain(Permission.MembersView);
    }

    [Fact]
    public async Task GetScopes_ACourtGrantLimitsToThatCourt_AndADenyRemovesOneFromTheRole()
    {
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court1Id, PermissionEffect.Allow, null));

        var onlyGrant = await QueryAsync(ctx => PermissionEvaluator.GetScopes(ctx, TestDataSeed.ClubId, TestDataSeed.Member1Id, [MemberRole.User], CancellationToken.None));

        onlyGrant.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PermissionScope(Permission.CourtBlockingsManage, PermissionResourceType.Court, [TestDataSeed.Court1Id], []));

        // A Maintainer may block every court by role: a deny takes one away, the allow for court 1 adds nothing
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court2Id, PermissionEffect.Deny, null));

        var withRole = await QueryAsync(ctx => PermissionEvaluator.GetScopes(ctx, TestDataSeed.ClubId, TestDataSeed.Member1Id, [MemberRole.Maintainer], CancellationToken.None));
        var asAdmin = await QueryAsync(ctx => PermissionEvaluator.GetScopes(ctx, TestDataSeed.ClubId, TestDataSeed.Member1Id, [MemberRole.Admin], CancellationToken.None));

        withRole.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PermissionScope(Permission.CourtBlockingsManage, PermissionResourceType.Court, null, [TestDataSeed.Court2Id]));
        asAdmin.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScopes_AGeneralAllowWithACourtDeny_LimitsOnlyThatCourt_AndAGeneralDenyHasNoScope()
    {
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, null, null, PermissionEffect.Allow, null));
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.CourtBlockingsManage, PermissionResourceType.Court, TestDataSeed.Court2Id, PermissionEffect.Deny, null));
        await SendAsync(new CreatePermissionGrant(TestDataSeed.ClubId, TestDataSeed.UserId, TestDataSeed.Member1Id, Permission.BookingsManageForAnyMember, null, null, PermissionEffect.Deny, null));

        var scopes = await QueryAsync(ctx => PermissionEvaluator.GetScopes(ctx, TestDataSeed.ClubId, TestDataSeed.Member1Id, [MemberRole.Trainer], CancellationToken.None));

        scopes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PermissionScope(Permission.CourtBlockingsManage, PermissionResourceType.Court, null, [TestDataSeed.Court2Id]),
            "the general deny of booking for others takes the permission away completely, there is nothing to limit");
    }

    [Fact]
    public async Task UpdateMemberRoles_TheLastAdminKeepsTheRole()
    {
        var act = () => SendAsync(new UpdateMemberRoles(TestDataSeed.Member2Id, [MemberRole.User]));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(UpdateMemberRoles.ErrorCode.LastAdminCannotBeRemoved));
        Query(ctx => ctx.TestData().Member2.UserRoles).Should().Contain(MemberRole.Admin);
    }

    [Fact]
    public async Task UpdateMemberRoles_AnAdminCanGiveUpTheRole_WhenAnotherAdminExists()
    {
        await SendAsync(new UpdateMemberRoles(TestDataSeed.Member1Id, [MemberRole.Admin]));

        await SendAsync(new UpdateMemberRoles(TestDataSeed.Member2Id, [MemberRole.User]));

        Query(ctx => ctx.TestData().Member2.UserRoles).Should().Equal(MemberRole.User);
    }

    [Fact]
    public async Task UpdateMemberRoles_KeepingTheAdminRole_IsAlwaysFine()
    {
        await SendAsync(new UpdateMemberRoles(TestDataSeed.Member2Id, [MemberRole.Admin, MemberRole.Treasurer]));

        Query(ctx => ctx.TestData().Member2.UserRoles).Should().BeEquivalentTo([MemberRole.Admin, MemberRole.Treasurer]);
    }
}
