using Bookennis.Client.Services.HttpClients.Members;
using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Client.Utils;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Services.Store.Members;

public class MembersStore(IMemberHttpService memberHttpService) : SemaphoreStore, IMembersStore
{
    private int? loadedMemberId;
    public event Action? OnMembersChanged;
    public event Action? OnMemberChanged;
    public event Action? OnBookingHistoryChanged;
    public event Action? OnSummaryChanged;
    public event Action? OnMemberOverviewChanged;
    public bool Loaded { get; private set; }
    public List<MemberResult> Members { get; private set; } = [];
    public int TotalMembers { get; private set; }
    public int Page { get; private set; }
    public MembersDetailResult? MemberDetail { get; private set; }
    public List<BookingResult> BookingHistory { get; private set; } = [];
    public GetMembersSummaryResult? Summary { get; private set; }
    public string? ListUri { get; set; }
    public GetMemberOverviewResult? MemberOverview { get; private set; }

    public Task LoadMembers(PaginationParameters paginationParameters, SortParameters? sortParameters, MemberFilter filter)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            Loaded = false;
            var membersResult = await memberHttpService.GetMembers(paginationParameters, sortParameters, filter, cancellationToken);
            if (membersResult is { Success: true, Dto: not null })
            {
                Members = membersResult.Dto.Entities;
                TotalMembers = membersResult.Dto.Total;
                Page = membersResult.Dto.Page;
                OnMembersChanged?.Invoke();
                Loaded = true;
            }
        }, nameof(LoadMembers));

    public async Task<string[]> GetMemberEmails(MemberFilter filter)
    {
        var result = await memberHttpService.GetMemberEmails(filter);
        return result is { Success: true, Dto: not null } ? result.Dto.Emails : [];
    }

    public Task LoadSummary()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await memberHttpService.GetMembersSummary(cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Summary = result.Dto;
                OnSummaryChanged?.Invoke();
            }
        }, nameof(LoadSummary));

    public Task LoadMemberOverview(int memberId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            MemberOverview = null;
            var result = await memberHttpService.GetMemberOverview(memberId, cancellationToken);
            if (result is { Success: true, Dto: not null })
                MemberOverview = result.Dto;

            OnMemberOverviewChanged?.Invoke();
        }, nameof(LoadMemberOverview));

    public Task LoadMember(int memberId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            loadedMemberId = null;
            Loaded = false;
            var memberResult = await memberHttpService.GetMember(memberId, cancellationToken);
            if (memberResult is { Success: true, Dto: not null })
            {
                MemberDetail = memberResult.Dto;
                OnMemberChanged?.Invoke();
                Loaded = true;
                loadedMemberId = memberId;
            }
        }, nameof(LoadMember));

    public bool LoadedMemberId(int memberId) => memberId == loadedMemberId;

    public void ResetMembers()
    {
        Members.Clear();
        Loaded = false;
        OnMembersChanged?.Invoke();
    }

    public void ResetMember()
    {
        loadedMemberId = null;
        MemberDetail = null;
        Loaded = false;
        OnMemberChanged?.Invoke();
    }

    public Task<HttpResult> UpdateMemberBookingOptions(int memberId, int[] allowedSeasonIds, int bookingsPerWeek)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await memberHttpService.UpdateMemberBookingOptions(memberId, allowedSeasonIds, bookingsPerWeek, cancellationToken);
            if (result is { Success: true })
            {
                UpdateMember(Members.SingleOrDefault(i => i.MemberId == memberId));
                UpdateMember(MemberDetail);
                OnMemberChanged?.Invoke();
            }

            return result;

            void UpdateMember(MemberResult? member)
            {
                if (member is not null)
                {
                    member.AllowedSeasonIds = allowedSeasonIds;
                    member.BookingsPerWeek = bookingsPerWeek;
                }
            }
        }, nameof(UpdateMemberBookingOptions));

    public Task<HttpResult> UpdateMemberRoles(int memberId, MemberRole[] roles)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await memberHttpService.UpdateMemberRoles(memberId, roles, cancellationToken);
            if (result is { Success: true })
            {
                UpdateMember(Members.SingleOrDefault(i => i.MemberId == memberId));
                UpdateMember(MemberDetail);
                OnMemberChanged?.Invoke();
            }

            return result;

            void UpdateMember(MemberResult? member) => member?.Roles = roles;
        }, nameof(UpdateMemberRoles));

    public Task LoadBookingHistory(int memberId, int seasonId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await memberHttpService.GetBookingHistory(memberId, seasonId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                BookingHistory = result.Dto.Bookings.AsLocalTime();
                OnBookingHistoryChanged?.Invoke();
            }
        }, nameof(LoadBookingHistory));
}