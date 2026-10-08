using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.BadgeTiers;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Client.Services.HttpClients.Badges;

public class BadgesHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IBadgesHttpClient
{
    public async Task<HttpResult<GetBadgeTiersResult>> GetBadgeTiers(int seasonId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"BadgeTiers?seasonId={seasonId}", cancellationToken)).AsHttpResult<GetBadgeTiersResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateBadgeTier(int seasonId, CreateBadgeTierModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync($"BadgeTiers?seasonId={seasonId}", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateBadgeTier(int tierId, UpdateBadgeTierModel model, CancellationToken cancellationToken)
        => await (await httpClient.PutAsJsonAsync($"BadgeTiers/{tierId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteBadgeTier(int tierId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"BadgeTiers/{tierId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> CopyBadgeTiers(CopyBadgeTiersModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("BadgeTiers/copy", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UploadBadgeTierImage(int tierId, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        return await (await httpClient.PostAsync($"BadgeTiers/{tierId}/image", content, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<GetMemberBadgeProgressResult>> GetMemberBadgeProgress(int memberId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"MemberBadges/Members/{memberId}/badge-progress", cancellationToken)).AsHttpResult<GetMemberBadgeProgressResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetMemberBadgesResult>> GetMemberBadges(int memberId, int? seasonId, CancellationToken cancellationToken)
    {
        var url = $"MemberBadges/Members/{memberId}/badges";
        if (seasonId.HasValue)
            url += $"?seasonId={seasonId.Value}";
        return await (await httpClient.GetAsync(url, cancellationToken)).AsHttpResult<GetMemberBadgesResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<GetTrophyCaseResult>> GetTrophyCase(int memberId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"MemberBadges/Members/{memberId}/trophy-case", cancellationToken)).AsHttpResult<GetTrophyCaseResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateMemberBadgeSettings(int memberId, UpdateMemberBadgeSettingsModel model, CancellationToken cancellationToken)
        => await (await httpClient.PutAsJsonAsync($"MemberBadges/Members/{memberId}/badge-settings", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetOneTimeBadgesResult>> GetOneTimeBadges(int seasonId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"OneTimeBadges?seasonId={seasonId}", cancellationToken)).AsHttpResult<GetOneTimeBadgesResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetSeasonClubMembersResult>> GetSeasonClubMembers(int seasonId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"OneTimeBadges/season-members?seasonId={seasonId}", cancellationToken)).AsHttpResult<GetSeasonClubMembersResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateOneTimeBadge(int seasonId, CreateOneTimeBadgeModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync($"OneTimeBadges?seasonId={seasonId}", model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateOneTimeBadge(int oneTimeBadgeId, UpdateOneTimeBadgeModel model, CancellationToken cancellationToken)
        => await (await httpClient.PutAsJsonAsync($"OneTimeBadges/{oneTimeBadgeId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteOneTimeBadge(int oneTimeBadgeId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"OneTimeBadges/{oneTimeBadgeId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> CopyOneTimeBadges(CopyOneTimeBadgesModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("OneTimeBadges/copy", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UploadOneTimeBadgeImage(int oneTimeBadgeId, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        return await (await httpClient.PostAsync($"OneTimeBadges/{oneTimeBadgeId}/image", content, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> AwardOneTimeBadge(int oneTimeBadgeId, AwardOneTimeBadgeModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync($"OneTimeBadges/{oneTimeBadgeId}/award", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> RevokeOneTimeBadge(int oneTimeBadgeId, int memberId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"OneTimeBadges/{oneTimeBadgeId}/award/{memberId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
