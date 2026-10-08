using Bookennis.Api.Business.Families;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class DeleteFamilyTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task DeleteFamily_RemovesFamilyAndItsMemberLinks_ButKeepsTheMembers()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var parent = await MemberSeed.AddUserWithMember(ctx, "parent@test.com");
            var otherParent = await MemberSeed.AddUserWithMember(ctx, "otherparent@test.com");
            var child = await MemberSeed.AddChild(ctx, parent.UserId, "Kid");
            var familyId = await MemberSeed.AddFamily(ctx, [parent.MemberId, otherParent.MemberId], [child.MemberId]);
            return new { FamilyId = familyId, MemberIds = new[] { parent.MemberId, otherParent.MemberId, child.MemberId } };
        });

        await SendAsync(new DeleteFamily(seeded.FamilyId));

        await QueryAsync(async ctx =>
        {
            (await ctx.Families.AnyAsync(f => f.Id == seeded.FamilyId)).Should().BeFalse();
            (await ctx.FamilyMembers.AnyAsync(f => f.FamilyId == seeded.FamilyId)).Should().BeFalse();
            (await ctx.ClubMembers.CountAsync(m => seeded.MemberIds.Contains(m.Id))).Should().Be(3);
        });
    }

    [Fact]
    public async Task DeleteFamily_OtherFamiliesAreKept()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            var parentA = await MemberSeed.AddUserWithMember(ctx, "parenta@test.com");
            var parentB = await MemberSeed.AddUserWithMember(ctx, "parentb@test.com");
            return new
            {
                FamilyToDelete = await MemberSeed.AddFamily(ctx, [parentA.MemberId], []),
                FamilyToKeep = await MemberSeed.AddFamily(ctx, [parentB.MemberId], [])
            };
        });

        await SendAsync(new DeleteFamily(seeded.FamilyToDelete));

        await QueryAsync(async ctx =>
        {
            (await ctx.Families.AnyAsync(f => f.Id == seeded.FamilyToDelete)).Should().BeFalse();
            (await ctx.Families.AnyAsync(f => f.Id == seeded.FamilyToKeep)).Should().BeTrue();
            (await ctx.FamilyMembers.AnyAsync(f => f.FamilyId == seeded.FamilyToKeep)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task DeleteFamily_UnknownFamily_Throws()
    {
        var act = () => SendAsync(new DeleteFamily(-1));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
