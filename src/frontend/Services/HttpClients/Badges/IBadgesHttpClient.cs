using Bookennis.Shared.Controller.BadgeTiers;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Client.Services.HttpClients.Badges;

public interface IBadgesHttpClient
{
    Task<HttpResult<GetBadgeTiersResult>> GetBadgeTiers(int seasonId, CancellationToken cancellationToken = default);
    Task<HttpResult<int>> CreateBadgeTier(int seasonId, CreateBadgeTierModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> UpdateBadgeTier(int tierId, UpdateBadgeTierModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> DeleteBadgeTier(int tierId, CancellationToken cancellationToken = default);
    Task<HttpResult> CopyBadgeTiers(CopyBadgeTiersModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> UploadBadgeTierImage(int tierId, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<HttpResult<GetMemberBadgeProgressResult>> GetMemberBadgeProgress(int memberId, CancellationToken cancellationToken = default);
    Task<HttpResult<GetMemberBadgesResult>> GetMemberBadges(int memberId, int? seasonId = null, CancellationToken cancellationToken = default);
    Task<HttpResult<GetTrophyCaseResult>> GetTrophyCase(int memberId, CancellationToken cancellationToken = default);
    Task<HttpResult> UpdateMemberBadgeSettings(int memberId, UpdateMemberBadgeSettingsModel model, CancellationToken cancellationToken = default);

    Task<HttpResult<GetOneTimeBadgesResult>> GetOneTimeBadges(int seasonId, CancellationToken cancellationToken = default);
    Task<HttpResult<GetSeasonClubMembersResult>> GetSeasonClubMembers(int seasonId, CancellationToken cancellationToken = default);
    Task<HttpResult<int>> CreateOneTimeBadge(int seasonId, CreateOneTimeBadgeModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> UpdateOneTimeBadge(int oneTimeBadgeId, UpdateOneTimeBadgeModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> DeleteOneTimeBadge(int oneTimeBadgeId, CancellationToken cancellationToken = default);
    Task<HttpResult> CopyOneTimeBadges(CopyOneTimeBadgesModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> UploadOneTimeBadgeImage(int oneTimeBadgeId, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<HttpResult> AwardOneTimeBadge(int oneTimeBadgeId, AwardOneTimeBadgeModel model, CancellationToken cancellationToken = default);
    Task<HttpResult> RevokeOneTimeBadge(int oneTimeBadgeId, int memberId, CancellationToken cancellationToken = default);
}
