using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.BadgeTiers;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Client.Services.Store.Badges;

public interface IBadgesStore : ISemaphoreStore
{
    event Action? OnBadgeTiersChanged;
    event Action? OnBadgeProgressChanged;
    event Action? OnTrophyCaseChanged;
    event Action? OnMyDisplayBadgeChanged;
    event Action? OnOneTimeBadgesChanged;

    GetBadgeTiersResult? BadgeTiers { get; }
    GetMemberBadgeProgressResult? BadgeProgress { get; }
    GetTrophyCaseResult? TrophyCase { get; }
    string? MyDisplayBadgeImageUrl { get; }
    int? MyDisplayBadgeLevel { get; }
    GetOneTimeBadgesResult? OneTimeBadges { get; }
    GetSeasonClubMembersResult? SeasonClubMembers { get; }

    Task LoadBadgeTiers(int seasonId);
    Task<HttpResult> CreateBadgeTier(int seasonId, CreateBadgeTierModel model);
    Task<HttpResult> UpdateBadgeTier(int tierId, UpdateBadgeTierModel model);
    Task<HttpResult> DeleteBadgeTier(int tierId);
    Task<HttpResult> CopyBadgeTiers(int sourceSeasonId, int targetSeasonId);
    Task<HttpResult> UploadBadgeTierImage(int tierId, Stream imageStream, string fileName, string contentType);
    Task LoadBadgeProgress(int memberId);
    Task<GetMemberBadgesResult?> LoadMemberBadges(int memberId, int? seasonId = null);
    Task LoadTrophyCase(int memberId);
    Task LoadMyDisplayBadge(int memberId);
    Task<HttpResult> UpdateBadgeSettings(int memberId, UpdateMemberBadgeSettingsModel model);

    Task LoadOneTimeBadges(int seasonId);
    Task LoadSeasonClubMembers(int seasonId);
    Task<HttpResult> CreateOneTimeBadge(int seasonId, CreateOneTimeBadgeModel model);
    Task<HttpResult> UpdateOneTimeBadge(int oneTimeBadgeId, UpdateOneTimeBadgeModel model);
    Task<HttpResult> DeleteOneTimeBadge(int oneTimeBadgeId);
    Task<HttpResult> CopyOneTimeBadges(int sourceSeasonId, int targetSeasonId);
    Task<HttpResult> UploadOneTimeBadgeImage(int oneTimeBadgeId, Stream imageStream, string fileName, string contentType);
    Task<HttpResult> AwardOneTimeBadge(int oneTimeBadgeId, List<int> memberIds);
    Task<HttpResult> RevokeOneTimeBadge(int oneTimeBadgeId, int memberId);
}
