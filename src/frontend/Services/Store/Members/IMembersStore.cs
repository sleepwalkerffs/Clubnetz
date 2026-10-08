using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Shared;
using Bookennis.Shared.Utils.Paging;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Services.Store.Members;

public interface IMembersStore : ISemaphoreStore
{
    public event Action? OnMembersChanged;
    public event Action? OnMemberChanged;
    public event Action? OnBookingHistoryChanged;
    public event Action? OnSummaryChanged;
    public event Action? OnMemberOverviewChanged;
    public List<MemberResult> Members { get; }
    public int TotalMembers { get; }
    public int Page { get; }
    public MembersDetailResult? MemberDetail { get; }
    public List<BookingResult> BookingHistory { get; }
    public GetMembersSummaryResult? Summary { get; }

    /// <summary>URL of the members list including its filter, to return to it from a member.</summary>
    public string? ListUri { get; set; }

    /// <summary>Overview of the member in <see cref="MemberDetail"/> (club administration only).</summary>
    public GetMemberOverviewResult? MemberOverview { get; }
    public Task LoadMembers(PaginationParameters paginationParameters, SortParameters? sortParameters, MemberFilter filter);
    public Task<string[]> GetMemberEmails(MemberFilter filter);
    public Task LoadSummary();
    public Task LoadMemberOverview(int memberId);
    public Task LoadMember(int memberId);
    public bool LoadedMemberId(int memberId);
    public void ResetMembers();
    public void ResetMember();
    public Task<HttpResult> UpdateMemberBookingOptions(int memberId, int[] allowedSeasonIds, int bookingsPerWeek);
    public Task<HttpResult> UpdateMemberRoles(int memberId, MemberRole[] roles);
    public Task LoadBookingHistory(int memberId, int seasonId);
}