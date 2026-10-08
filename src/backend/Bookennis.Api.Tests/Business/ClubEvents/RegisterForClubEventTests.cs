using Bookennis.Api.Business.ClubEvents;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubEvents;

public class RegisterForClubEventTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task RegisterForClubEvent_CreatesRegistration()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx));

        var result = await SendAsync(new RegisterForClubEvent(
            TestDataSeed.ClubId,
            TestDataSeed.UserId,
            clubEvent.Id,
            3,
            "See you",
            [new(ClubEventSeed.Option(clubEvent, "Yes"), 1), new(ClubEventSeed.Option(clubEvent, "Schnitzel"), 2)]));

        result.TotalHeadCount.Should().Be(3);
        result.MyRegistration.Should().NotBeNull();
        result.MyRegistration!.HeadCount.Should().Be(3);
        result.MyRegistration.FirstName.Should().Be("us");

        var registration = await QueryAsync(ctx => ctx.ClubEventRegistrations.Include(r => r.Answers).SingleAsync());
        registration.MemberId.Should().Be(TestDataSeed.Member1Id);
        registration.Comment.Should().Be("See you");
        registration.Answers.Should().HaveCount(2);
    }

    [Fact]
    public async Task RegisterForClubEvent_Again_UpdatesRegistration()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx));

        var result = await SendAsync(new RegisterForClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id, 2, null, [new(ClubEventSeed.Option(clubEvent, "No"), 1)]));

        result.MyRegistration!.HeadCount.Should().Be(2);
        result.MyRegistration.Answers.Should().ContainSingle().Which.OptionId.Should().Be(ClubEventSeed.Option(clubEvent, "No"));
        (await QueryAsync(ctx => ctx.ClubEventRegistrations.CountAsync())).Should().Be(2);
        (await QueryAsync(ctx => ctx.ClubEventRegistrationAnswers.CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task RegisterForClubEvent_UnlimitedQuantity_StoresQuantityAboveHeadCount()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data() with
        {
            Questions = [new(null, "Drinks", ClubEventQuestionSelectionMode.MultipleChoice, false, true, [new(null, "Beer"), new(null, "Water")], LimitQuantityToHeadCount: false)],
        }));

        var result = await SendAsync(new RegisterForClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id, 2, null, [new(ClubEventSeed.Option(clubEvent, "Beer"), 8)]));

        result.Questions.Single().LimitQuantityToHeadCount.Should().BeFalse();
        result.Questions.Single().Options.Single(o => o.Label == "Beer").Total.Should().Be(8);
    }

    [Fact]
    public async Task RegisterForClubEvent_ExceedsCapacity_ThrowsPreconditionException()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEventWithRegistrations(ctx, maxParticipants: 5));

        // Member1 already has 3 people, Member2 has 1 → at most 4 for Member1
        var act = () => SendAsync(new RegisterForClubEvent(TestDataSeed.ClubId, TestDataSeed.UserId, clubEvent.Id, 5, null, [new(ClubEventSeed.Option(clubEvent, "Yes"), 1)]));

        (await act.Should().ThrowAsync<PreconditionException>())
            .Which.ErrorCode.Should().Be(nameof(ClubEvent.ErrorCode.ClubEventFull));
    }

    [Fact]
    public async Task RegisterForClubEvent_GuestMemberOnly_ThrowsNotFound()
    {
        var clubEvent = await QueryAsync(ctx => ClubEventSeed.SeedEvent(ctx, ClubEventSeed.Data(withQuestions: false)));
        var guestUserId = await QueryAsync(async ctx =>
        {
            var guestUser = new User("eventguest@test.com", "eventguest@test.com", "Guest", "Player", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(guestUser);
            await ctx.SaveChangesAsync();
            ctx.Add(new GuestMember(guestUser.Id, TestDataSeed.ClubId));
            await ctx.SaveChangesAsync();
            return guestUser.Id;
        });

        var act = () => SendAsync(new RegisterForClubEvent(TestDataSeed.ClubId, guestUserId, clubEvent.Id, 1, null, []));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
