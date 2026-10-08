using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubEvents;
using Microsoft.EntityFrameworkCore;
using ClubEventCategory = Bookennis.Shared.Controller.ClubEvents.ClubEventCategory;
using ClubEventQuestionSelectionMode = Bookennis.Shared.Controller.ClubEvents.ClubEventQuestionSelectionMode;

namespace Bookennis.Api.Business.ClubEvents;

public static class ClubEventMapper
{
    public record MemberInfo(int MemberId, string FirstName, string LastName, string? ProfilePictureUrl);

    /// <summary>Loads the names of the event's creator and all registered members.</summary>
    public static Task<Dictionary<int, MemberInfo>> LoadMemberInfos(AppDbContext context, ClubEvent clubEvent, CancellationToken cancellationToken)
    {
        var memberIds = clubEvent.Registrations.Select(r => r.MemberId).ToList();
        if (clubEvent.CreatedByMemberId.HasValue)
            memberIds.Add(clubEvent.CreatedByMemberId.Value);

        return (from member in context.Set<Member>()
                join user in context.Users on member.UserId equals user.Id
                where memberIds.Contains(member.Id)
                select new MemberInfo(
                    member.Id,
                    user.FirstName,
                    user.LastName,
                    context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null))
            .ToDictionaryAsync(m => m.MemberId, cancellationToken);
    }

    public static ClubEventDto ToDto(IClubEmailRenderer renderer, ClubEvent clubEvent, IReadOnlyDictionary<int, MemberInfo> members, int? myMemberId, DateTimeOffset now)
    {
        var quantitiesByOption = clubEvent.Registrations
            .SelectMany(r => r.Answers)
            .GroupBy(a => a.ClubEventQuestionOptionId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Quantity));

        var registrations = clubEvent.Registrations
            .Select(r =>
            {
                var member = members.GetValueOrDefault(r.MemberId);
                return new ClubEventRegistrationDto
                {
                    Id = r.Id,
                    MemberId = r.MemberId,
                    FirstName = member?.FirstName ?? "",
                    LastName = member?.LastName ?? "",
                    ProfilePictureUrl = member?.ProfilePictureUrl,
                    HeadCount = r.HeadCount,
                    Comment = r.Comment,
                    RegisteredAt = r.RegisteredAt,
                    Answers = r.Answers.Select(a => new ClubEventAnswerDto(a.ClubEventQuestionOptionId, a.Quantity)).ToList(),
                };
            })
            .OrderBy(r => r.RegisteredAt)
            .ThenBy(r => r.Id)
            .ToList();

        var creator = clubEvent.CreatedByMemberId.HasValue ? members.GetValueOrDefault(clubEvent.CreatedByMemberId.Value) : null;

        return new ClubEventDto
        {
            Id = clubEvent.Id,
            Title = clubEvent.Title,
            Description = clubEvent.Description,
            DescriptionHtml = clubEvent.Description is null ? null : renderer.RenderMarkdown(clubEvent.Description),
            Location = clubEvent.Location,
            Category = (ClubEventCategory)clubEvent.Category,
            StartDate = clubEvent.StartDate,
            EndDate = clubEvent.EndDate,
            StartTime = clubEvent.StartTime,
            EndTime = clubEvent.EndTime,
            RegistrationEnabled = clubEvent.RegistrationEnabled,
            MaxParticipants = clubEvent.MaxParticipants,
            RegistrationDeadline = clubEvent.RegistrationDeadline,
            NotifyMembers = clubEvent.NotifyMembers,
            CreatedByName = creator is null ? null : $"{creator.FirstName} {creator.LastName}",
            IsRegistrationOpen = clubEvent.IsRegistrationOpen(now),
            IsDeadlinePassed = clubEvent.IsDeadlinePassed(now),
            IsOver = clubEvent.IsOver(now),
            IsFull = clubEvent.IsFull,
            TotalHeadCount = clubEvent.TotalHeadCount,
            Questions = clubEvent.Questions
                .OrderBy(q => q.SortOrder)
                .Select(q => new ClubEventQuestionDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    SelectionMode = (ClubEventQuestionSelectionMode)q.SelectionMode,
                    IsRequired = q.IsRequired,
                    AllowQuantities = q.AllowQuantities,
                    LimitQuantityToHeadCount = q.LimitQuantityToHeadCount,
                    Options = q.Options
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new ClubEventOptionDto { Id = o.Id, Label = o.Label, Total = quantitiesByOption.GetValueOrDefault(o.Id) })
                        .ToList(),
                })
                .ToList(),
            Registrations = registrations,
            MyRegistration = myMemberId.HasValue ? registrations.FirstOrDefault(r => r.MemberId == myMemberId.Value) : null,
        };
    }

    public static ClubEvent.EventData ToEventData(SaveClubEventModel model)
        => new(
            model.Title ?? "",
            model.Description,
            model.Location,
            (Domain.ClubEvents.ClubEventCategory)model.Category,
            model.StartDate,
            model.EndDate,
            model.StartTime,
            model.EndTime,
            model.RegistrationEnabled,
            model.MaxParticipants,
            model.RegistrationDeadline,
            model.Questions
                .Select(q => new ClubEvent.QuestionData(
                    q.Id,
                    q.Text ?? "",
                    (Domain.ClubEvents.ClubEventQuestionSelectionMode)q.SelectionMode,
                    q.IsRequired,
                    q.AllowQuantities,
                    q.Options.Select(o => new ClubEvent.OptionData(o.Id, o.Label ?? "")).ToList(),
                    q.LimitQuantityToHeadCount))
                .ToList(),
            model.NotifyMembers);
}
