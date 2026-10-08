using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Admin;

[Authorize(AuthorizationPolicies.ApplicationAdministrator)]
[Route("api/Admin/[controller]")]
public class ClubsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<List<AdminClubResult>> GetClubs(CancellationToken cancellationToken)
        => mediator.Send(new GetAdminClubs(), cancellationToken);

    [HttpGet("{clubId:int}")]
    public Task<AdminClubDetailResult> GetClub(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminClub(clubId), cancellationToken);

    [HttpPost]
    public Task<int> CreateClub(CreateClubRequest request, CancellationToken cancellationToken)
        => mediator.Send(new CreateAdminClub(request.Name, request.OpeningHours, request.PrimeTimeHours, request.BookingGracePeriodInMinutes), cancellationToken);

    [HttpPut("{clubId:int}")]
    public Task UpdateClub(int clubId, UpdateAdminClubRequest request, CancellationToken cancellationToken)
        => mediator.Send(
            new UpdateAdminClub(
                clubId,
                request.Name,
                request.OpeningHours,
                request.PrimeTimeSettings.PrimeTimeHours,
                new PrimeTimeSettings(
                    request.PrimeTimeSettings.IsEnabled,
                    request.PrimeTimeSettings.PrimeTimeHours,
                    request.PrimeTimeSettings.ApplicableWeekdays,
                    request.PrimeTimeSettings.RestrictChildren,
                    request.PrimeTimeSettings.RestrictGuests,
                    request.PrimeTimeSettings.ChildAgeThreshold),
                request.BookingGracePeriodInMinutes,
                request.IsAtpClub),
            cancellationToken);

    [HttpDelete("{clubId:int}")]
    public Task DeleteClub(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteAdminClub(clubId), cancellationToken);

    // PlayModes
    [HttpPost("{clubId:int}/PlayModes")]
    public Task AddPlayMode(int clubId, AdminPlayModeRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminAddPlayMode(clubId, request.Name, request.AllowedRoles.Select(r => (MemberRole)r).ToArray(), request.Color, request.FixedPlayerCount, request.IsChargingBookingSubscription, request.FixedDuration, request.CanOverbook, request.CommentAllowed, request.MaxBookingsPerSeason, request.AllowRecurring), cancellationToken);

    [HttpPut("{clubId:int}/PlayModes/{playModeId:int}")]
    public Task UpdatePlayMode(int clubId, int playModeId, AdminPlayModeRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminUpdatePlayMode(clubId, playModeId, request.Name, request.AllowedRoles.Select(r => (MemberRole)r).ToArray(), request.Color, request.FixedPlayerCount, request.IsChargingBookingSubscription, request.FixedDuration, request.CanOverbook, request.CommentAllowed, request.MaxBookingsPerSeason, request.AllowRecurring), cancellationToken);

    [HttpDelete("{clubId:int}/PlayModes/{playModeId:int}")]
    public Task DeletePlayMode(int clubId, int playModeId, CancellationToken cancellationToken)
        => mediator.Send(new AdminDeletePlayMode(clubId, playModeId), cancellationToken);

    // Members
    [HttpGet("{clubId:int}/Members")]
    public Task<GetAdminClubMembersResult> GetMembers(int clubId, [FromQuery] PaginationParameters pagination, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminClubMembers(clubId, pagination, searchTerm), cancellationToken);

    [HttpPost("{clubId:int}/Members")]
    public Task AddMember(int clubId, AddClubMemberRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminAddClubMember(clubId, request.UserId, request.Roles.Select(r => (MemberRole)r).ToArray()), cancellationToken);

    [HttpPut("{clubId:int}/Members/{memberId:int}")]
    public Task UpdateMember(int clubId, int memberId, UpdateClubMemberRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminUpdateClubMember(clubId, memberId, request.Roles.Select(r => (MemberRole)r).ToArray(), request.AllowedSeasonIds, request.BookingsPerWeek), cancellationToken);

    [HttpDelete("{clubId:int}/Members/{memberId:int}")]
    public Task RemoveMember(int clubId, int memberId, CancellationToken cancellationToken)
        => mediator.Send(new AdminRemoveClubMember(clubId, memberId), cancellationToken);

    // Courts
    [HttpGet("{clubId:int}/Courts")]
    public Task<List<AdminCourtResult>> GetCourts(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminClubCourts(clubId), cancellationToken);

    [HttpPost("{clubId:int}/Courts")]
    public Task AddCourt(int clubId, AdminCourtRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminAddCourt(clubId, request.Name, request.Alias, request.SortOrder), cancellationToken);

    [HttpPut("{clubId:int}/Courts/{courtId:int}")]
    public Task UpdateCourt(int clubId, int courtId, AdminCourtRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminUpdateCourt(clubId, courtId, request.Name, request.Alias, request.SortOrder, request.Inactive), cancellationToken);

    [HttpDelete("{clubId:int}/Courts/{courtId:int}")]
    public Task DeleteCourt(int clubId, int courtId, CancellationToken cancellationToken)
        => mediator.Send(new AdminDeleteCourt(clubId, courtId), cancellationToken);

    // Families
    [HttpGet("{clubId:int}/Families")]
    public Task<GetAdminClubFamiliesResult> GetFamilies(int clubId, [FromQuery] PaginationParameters pagination, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminClubFamilies(clubId, pagination, searchTerm), cancellationToken);

    [HttpPut("{clubId:int}/Families/Members/{memberId:int}")]
    public Task UpdateFamilyMember(int clubId, int memberId, UpdateFamilyMemberRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminUpdateFamilyMember(clubId, memberId, request.FirstName, request.LastName, request.Birthday, (Gender)request.Gender), cancellationToken);

    // Seasons
    [HttpGet("{clubId:int}/Seasons")]
    public Task<List<AdminSeasonResult>> GetSeasons(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminClubSeasons(clubId), cancellationToken);

    [HttpPost("{clubId:int}/Seasons")]
    public Task<int> AddSeason(int clubId, AdminSeasonRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminAddSeason(clubId, request.StartDate, request.EndDate), cancellationToken);

    [HttpPut("{clubId:int}/Seasons/{seasonId:int}")]
    public Task UpdateSeason(int clubId, int seasonId, AdminSeasonRequest request, CancellationToken cancellationToken)
        => mediator.Send(new AdminUpdateSeason(clubId, seasonId, request.StartDate, request.EndDate), cancellationToken);

    [HttpDelete("{clubId:int}/Seasons/{seasonId:int}")]
    public Task DeleteSeason(int clubId, int seasonId, CancellationToken cancellationToken)
        => mediator.Send(new AdminDeleteSeason(clubId, seasonId), cancellationToken);
}
