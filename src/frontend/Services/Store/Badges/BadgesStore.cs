using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Badges;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.BadgeTiers;
using Bookennis.Shared.Controller.MemberBadges;
using Bookennis.Shared.Controller.OneTimeBadges;

namespace Bookennis.Client.Services.Store.Badges;

public sealed class BadgesStore(IBadgesHttpClient httpClient) : SemaphoreStore, IBadgesStore
{
    public event Action? OnBadgeTiersChanged;
    public event Action? OnBadgeProgressChanged;
    public event Action? OnTrophyCaseChanged;
    public event Action? OnMyDisplayBadgeChanged;
    public event Action? OnOneTimeBadgesChanged;

    public GetBadgeTiersResult? BadgeTiers { get; private set; }
    private int? loadedBadgeTiersSeasonId;
    public GetMemberBadgeProgressResult? BadgeProgress { get; private set; }
    public GetTrophyCaseResult? TrophyCase { get; private set; }
    public string? MyDisplayBadgeImageUrl { get; private set; }
    public int? MyDisplayBadgeLevel { get; private set; }
    public GetOneTimeBadgesResult? OneTimeBadges { get; private set; }
    private int? loadedOneTimeBadgesSeasonId;
    public GetSeasonClubMembersResult? SeasonClubMembers { get; private set; }

    public Task LoadBadgeTiers(int seasonId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            loadedBadgeTiersSeasonId = seasonId;
            var result = await httpClient.GetBadgeTiers(seasonId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                BadgeTiers = result.Dto;
                OnBadgeTiersChanged?.Invoke();
            }
        }, nameof(LoadBadgeTiers));

    public Task<HttpResult> CreateBadgeTier(int seasonId, CreateBadgeTierModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.CreateBadgeTier(seasonId, model, cancellationToken);
            if (result.Success)
                await LoadBadgeTiers(seasonId);
            return result;
        }, nameof(CreateBadgeTier));

    public Task<HttpResult> UpdateBadgeTier(int tierId, UpdateBadgeTierModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UpdateBadgeTier(tierId, model, cancellationToken);
            if (result.Success && loadedBadgeTiersSeasonId is { } seasonId)
                await LoadBadgeTiers(seasonId);
            return result;
        }, nameof(UpdateBadgeTier));

    public Task<HttpResult> DeleteBadgeTier(int tierId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeleteBadgeTier(tierId, cancellationToken);
            if (result.Success && loadedBadgeTiersSeasonId is { } seasonId)
                await LoadBadgeTiers(seasonId);
            return result;
        }, nameof(DeleteBadgeTier));

    public Task<HttpResult> CopyBadgeTiers(int sourceSeasonId, int targetSeasonId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.CopyBadgeTiers(new CopyBadgeTiersModel(sourceSeasonId, targetSeasonId), cancellationToken);
            if (result.Success)
                await LoadBadgeTiers(targetSeasonId);
            return result;
        }, nameof(CopyBadgeTiers));

    public Task<HttpResult> UploadBadgeTierImage(int tierId, Stream imageStream, string fileName, string contentType)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UploadBadgeTierImage(tierId, imageStream, fileName, contentType, cancellationToken);
            if (result.Success && loadedBadgeTiersSeasonId is { } seasonId)
                await LoadBadgeTiers(seasonId);
            return result;
        }, nameof(UploadBadgeTierImage));

    public Task LoadBadgeProgress(int memberId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.GetMemberBadgeProgress(memberId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                BadgeProgress = result.Dto;
                OnBadgeProgressChanged?.Invoke();
            }
        }, nameof(LoadBadgeProgress));

    public async Task<GetMemberBadgesResult?> LoadMemberBadges(int memberId, int? seasonId)
    {
        var result = await httpClient.GetMemberBadges(memberId, seasonId);
        return result is { Success: true, Dto: not null } ? result.Dto : null;
    }

    public Task LoadTrophyCase(int memberId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.GetTrophyCase(memberId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                TrophyCase = result.Dto;
                OnTrophyCaseChanged?.Invoke();
            }
        }, nameof(LoadTrophyCase));

    public Task LoadMyDisplayBadge(int memberId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.GetTrophyCase(memberId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                MyDisplayBadgeImageUrl = result.Dto.DisplayBadgeImageUrl;
                MyDisplayBadgeLevel = result.Dto.DisplayBadgeLevel;
                OnMyDisplayBadgeChanged?.Invoke();
            }
        }, nameof(LoadMyDisplayBadge));

    public Task<HttpResult> UpdateBadgeSettings(int memberId, UpdateMemberBadgeSettingsModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UpdateMemberBadgeSettings(memberId, model, cancellationToken);
            if (result.Success)
                await LoadMyDisplayBadge(memberId);
            return result;
        }, nameof(UpdateBadgeSettings));

    public Task LoadOneTimeBadges(int seasonId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            loadedOneTimeBadgesSeasonId = seasonId;
            var result = await httpClient.GetOneTimeBadges(seasonId, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                OneTimeBadges = result.Dto;
                OnOneTimeBadgesChanged?.Invoke();
            }
        }, nameof(LoadOneTimeBadges));

    public Task LoadSeasonClubMembers(int seasonId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.GetSeasonClubMembers(seasonId, cancellationToken);
            if (result is { Success: true, Dto: not null })
                SeasonClubMembers = result.Dto;
        }, nameof(LoadSeasonClubMembers));

    public Task<HttpResult> CreateOneTimeBadge(int seasonId, CreateOneTimeBadgeModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.CreateOneTimeBadge(seasonId, model, cancellationToken);
            if (result.Success)
                await LoadOneTimeBadges(seasonId);
            return result;
        }, nameof(CreateOneTimeBadge));

    public Task<HttpResult> UpdateOneTimeBadge(int oneTimeBadgeId, UpdateOneTimeBadgeModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UpdateOneTimeBadge(oneTimeBadgeId, model, cancellationToken);
            if (result.Success && loadedOneTimeBadgesSeasonId is { } seasonId)
                await LoadOneTimeBadges(seasonId);
            return result;
        }, nameof(UpdateOneTimeBadge));

    public Task<HttpResult> DeleteOneTimeBadge(int oneTimeBadgeId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeleteOneTimeBadge(oneTimeBadgeId, cancellationToken);
            if (result.Success && loadedOneTimeBadgesSeasonId is { } seasonId)
                await LoadOneTimeBadges(seasonId);
            return result;
        }, nameof(DeleteOneTimeBadge));

    public Task<HttpResult> CopyOneTimeBadges(int sourceSeasonId, int targetSeasonId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.CopyOneTimeBadges(new CopyOneTimeBadgesModel(sourceSeasonId, targetSeasonId), cancellationToken);
            if (result.Success)
                await LoadOneTimeBadges(targetSeasonId);
            return result;
        }, nameof(CopyOneTimeBadges));

    public Task<HttpResult> UploadOneTimeBadgeImage(int oneTimeBadgeId, Stream imageStream, string fileName, string contentType)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UploadOneTimeBadgeImage(oneTimeBadgeId, imageStream, fileName, contentType, cancellationToken);
            if (result.Success && loadedOneTimeBadgesSeasonId is { } seasonId)
                await LoadOneTimeBadges(seasonId);
            return result;
        }, nameof(UploadOneTimeBadgeImage));

    public Task<HttpResult> AwardOneTimeBadge(int oneTimeBadgeId, List<int> memberIds)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.AwardOneTimeBadge(oneTimeBadgeId, new AwardOneTimeBadgeModel(memberIds), cancellationToken);
            if (result.Success && loadedOneTimeBadgesSeasonId is { } seasonId)
                await LoadOneTimeBadges(seasonId);
            return result;
        }, nameof(AwardOneTimeBadge));

    public Task<HttpResult> RevokeOneTimeBadge(int oneTimeBadgeId, int memberId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.RevokeOneTimeBadge(oneTimeBadgeId, memberId, cancellationToken);
            if (result.Success && loadedOneTimeBadgesSeasonId is { } seasonId)
                await LoadOneTimeBadges(seasonId);
            return result;
        }, nameof(RevokeOneTimeBadge));
}
