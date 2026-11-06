using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Domain.PlayRequests;
using Bookennis.Shared.Controller.PlayRequests;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.PlayRequests;

/// <summary>
/// The requests the caller is part of - nobody else's: their own (recent ones and all open ones) and the open requests they were asked for.
/// A member who was asked does not see requests that were cancelled or expired, nor filled ones unless they play, and only gets the names
/// of the players who accepted. The requester sees everybody who was asked and what they answered.
/// </summary>
public record GetPlayRequests(int ClubId, int UserId) : IQuery<List<PlayRequestDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPlayRequests, List<PlayRequestDto>>
    {
        public async Task<List<PlayRequestDto>> Handle(GetPlayRequests request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var since = now.AddDays(-1);

            var requests = await context.PlayRequests
                .Include(r => r.Invitees)
                .Where(r => r.ClubId == request.ClubId
                    && r.StartsAt >= since
                    && (r.RequesterMemberId == memberId || r.Invitees.Any(i => i.MemberId == memberId)))
                .OrderBy(r => r.StartsAt)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var visible = requests.Where(r => IsVisible(r, memberId, now)).ToList();
            if (visible.Count == 0)
                return [];

            var memberIds = visible.SelectMany(r => r.Invitees.Select(i => i.MemberId).Append(r.RequesterMemberId)).Distinct().ToList();
            var names = await (
                from member in context.ClubMembers
                join user in context.Users on member.UserId equals user.Id
                where member.ClubId == request.ClubId && memberIds.Contains(member.Id)
                select new { member.Id, user.FirstName, user.LastName }
            ).ToDictionaryAsync(m => m.Id, m => (m.FirstName, m.LastName), cancellationToken);

            var courtIds = visible.SelectMany(r => r.CourtIds).Distinct().ToList();
            var courts = await context.Courts
                .Where(c => c.ClubId == request.ClubId && courtIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

            var playModeIds = visible.Select(r => r.PlayModeId).Distinct().ToList();
            var playModes = await context.PlayModes
                .Where(p => p.ClubId == request.ClubId && playModeIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

            return visible.ConvertAll(r => ToDto(r, memberId, now, names, courts, playModes));
        }

        internal static bool IsVisible(PlayRequest request, int memberId, DateTimeOffset now)
        {
            if (request.RequesterMemberId == memberId)
                return true;

            var mine = request.Invitees.FirstOrDefault(i => i.MemberId == memberId);
            if (mine is null)
                return false;

            return request.Status switch
            {
                PlayRequestStatus.Open => now < request.ExpiresAt,
                PlayRequestStatus.Filled => mine.Status == PlayRequestInviteeStatus.Accepted,
                _ => false,
            };
        }

        private static PlayRequestDto ToDto(
            PlayRequest request,
            int memberId,
            DateTimeOffset now,
            Dictionary<int, (string FirstName, string LastName)> names,
            Dictionary<int, string> courts,
            Dictionary<int, string> playModes)
        {
            var isMine = request.RequesterMemberId == memberId;
            var requester = names.GetValueOrDefault(request.RequesterMemberId);

            // Somebody who was asked only learns who plays, not who was asked or who said no
            var shownInvitees = request.Invitees
                .Where(i => isMine || i.Status == PlayRequestInviteeStatus.Accepted)
                .Select(i =>
                {
                    var name = names.GetValueOrDefault(i.MemberId);
                    return new PlayRequestInviteeDto(i.MemberId, name.FirstName ?? "", name.LastName ?? "", (PlayRequestAnswer)(int)i.Status);
                })
                .OrderBy(i => i.Answer == PlayRequestAnswer.Accepted ? 0 : i.Answer == PlayRequestAnswer.Invited ? 1 : 2)
                .ThenBy(i => i.LastName)
                .ThenBy(i => i.FirstName)
                .ToList();

            return new PlayRequestDto
            {
                Id = request.Id,
                RequesterMemberId = request.RequesterMemberId,
                RequesterFirstName = requester.FirstName ?? "",
                RequesterLastName = requester.LastName ?? "",
                PlayModeId = request.PlayModeId,
                PlayModeName = playModes.GetValueOrDefault(request.PlayModeId, ""),
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                TimeZoneInfoId = request.TimeZoneInfoId,
                Courts = request.CourtIds.Where(courts.ContainsKey).Select(id => new PlayRequestCourtDto(id, courts[id])).ToList(),
                PlayersNeeded = request.PlayersNeeded,
                OpenSpots = request.OpenSpots,
                Comment = request.Comment,
                ExpiresAt = request.ExpiresAt,
                State = request.Status switch
                {
                    PlayRequestStatus.Filled => PlayRequestState.Filled,
                    PlayRequestStatus.Cancelled => PlayRequestState.Cancelled,
                    _ => now < request.ExpiresAt ? PlayRequestState.Open : PlayRequestState.Expired,
                },
                BookingId = request.BookingId,
                IsMine = isMine,
                MyAnswer = isMine ? null : (PlayRequestAnswer)(int)request.Invitees.First(i => i.MemberId == memberId).Status,
                Invitees = shownInvitees,
            };
        }
    }
}

/// <summary>The members the caller can ask: active members of the club (name only), without the caller.</summary>
public record GetPlayRequestCandidates(int ClubId, int UserId) : IQuery<List<PlayRequestCandidateDto>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPlayRequestCandidates, List<PlayRequestCandidateDto>>
    {
        public async Task<List<PlayRequestCandidateDto>> Handle(GetPlayRequestCandidates request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var activeIds = await ActiveMembership.ActiveMemberIds(context, request.ClubId, null, cancellationToken);
            activeIds.IntersectWith(await PlayRequestBooking.WithOwnLogin(context, [.. activeIds], cancellationToken));
            activeIds.Remove(memberId);

            var members = await (
                from member in context.ClubMembers
                join user in context.Users on member.UserId equals user.Id
                where member.ClubId == request.ClubId && activeIds.Contains(member.Id)
                orderby user.LastName, user.FirstName
                select new PlayRequestCandidateDto(member.Id, user.FirstName, user.LastName)
            ).ToListAsync(cancellationToken);

            return members;
        }
    }
}
