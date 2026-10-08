using Bookennis.Api.Data;
using Bookennis.Domain.ClubEvents;

namespace Bookennis.Api.Tests.Business.ClubEvents;

internal static class ClubEventSeed
{
    public static readonly DateOnly EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

    public static ClubEvent.EventData Data(DateOnly? date = null, int? maxParticipants = null, bool withQuestions = true) => new(
        "Work effort",
        "Clean the courts",
        "Clubhouse",
        ClubEventCategory.WorkEffort,
        date ?? EventDate,
        null,
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        true,
        maxParticipants,
        null,
        withQuestions
            ?
            [
                new(null, "Staying for food?", ClubEventQuestionSelectionMode.SingleChoice, true, false, [new(null, "Yes"), new(null, "No")]),
                new(null, "Food", ClubEventQuestionSelectionMode.MultipleChoice, false, true, [new(null, "Schnitzel"), new(null, "Veggie")]),
            ]
            : []);

    public static async Task<ClubEvent> SeedEvent(AppDbContext context, ClubEvent.EventData? data = null)
    {
        var clubEvent = new ClubEvent(TestDataSeed.ClubId, TestDataSeed.Member2Id, data ?? Data());
        context.ClubEvents.Add(clubEvent);
        await context.SaveChangesAsync();
        return clubEvent;
    }

    /// <summary>An event where Member1 registered 3 people (2x Schnitzel, 1x Veggie) and Member2 registered 1 person without food.</summary>
    public static async Task<ClubEvent> SeedEventWithRegistrations(AppDbContext context, int? maxParticipants = null)
    {
        var clubEvent = await SeedEvent(context, Data(maxParticipants: maxParticipants));
        var now = DateTimeOffset.UtcNow;

        clubEvent.Register(TestDataSeed.Member1Id, 3, [new(Option(clubEvent, "Yes"), 1), new(Option(clubEvent, "Schnitzel"), 2), new(Option(clubEvent, "Veggie"), 1)], "Bringing a cake", now);
        clubEvent.Register(TestDataSeed.Member2Id, 1, [new(Option(clubEvent, "No"), 1)], null, now.AddMinutes(1));
        await context.SaveChangesAsync();

        return clubEvent;
    }

    public static int Option(ClubEvent clubEvent, string label)
        => clubEvent.Questions.SelectMany(q => q.Options).Single(o => o.Label == label).Id;
}
