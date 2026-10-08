using Bookennis.Client.Services.HttpClients.Family;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Families;

namespace Bookennis.Client.Services.Store.Family;

public class FamilyStore(IFamiliesHttpClient familiesHttpClient) : SemaphoreStore, IFamilyStore
{
    public event Action? OnFamilyChanged;
    public bool Loaded { get; private set; }
    public FamilyDto? Family { get; set; }
    public List<FamilyMemberDto> AvailableFamilyMembers { get; set; } = new();

    public bool LoadedForFamilyId(int familyId) => Family?.FamilyId == familyId;

    public void ResetFamily()
    {
        Loaded = false;
        Family = null;
    }

    public Task LoadFamily(int familyId) => RunInLoadingContextAsync(async cancellationToken =>
    {
        Family = null;
        Loaded = false;
        var familyResult = await familiesHttpClient.GetFamily(familyId, cancellationToken);
        if (familyResult is { Success: true, Dto: not null })
        {
            Family = familyResult.Dto;
            OnFamilyChanged?.Invoke();
            Loaded = true;
        }
    }, nameof(LoadFamily));

    public Task LoadAvailableFamilyMembers(int memberId)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            AvailableFamilyMembers = new List<FamilyMemberDto>();
            var availableFamilyMembersResponse = await familiesHttpClient.GetAvailableFamilyMembers(memberId, cancellationToken);
            if (availableFamilyMembersResponse is { Success: true, Dto: not null })
            {
                AvailableFamilyMembers = availableFamilyMembersResponse.Dto.AvailableMembers;
                OnFamilyChanged?.Invoke();
            }
        }, nameof(LoadAvailableFamilyMembers));

    public Task CreateFamily(CreateFamilyRequest request)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var createResponse = await familiesHttpClient.CreateFamily(request, cancellationToken);
            if (createResponse is { Success: true })
            {
                var familyId = createResponse.Dto;
                await LoadFamily(familyId);
                OnFamilyChanged?.Invoke();
            }
        }, nameof(CreateFamily));

    public Task AddParent(int memberId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            if (Family is null)
                return;

            var createResponse = await familiesHttpClient.AddParent(Family.FamilyId, new AddParentRequest(memberId), cancellationToken);
            if (createResponse is { Success: true })
            {
                await LoadFamily(Family.FamilyId);
                OnFamilyChanged?.Invoke();
            }
        }, nameof(AddParent));

    public Task AddNewChild(AddNewChildRequest request)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            if (Family is null)
                return;

            var createResponse = await familiesHttpClient.AddNewChild(Family.FamilyId, request, cancellationToken);
            if (createResponse is { Success: true })
            {
                await LoadFamily(Family.FamilyId);
                OnFamilyChanged?.Invoke();
            }
        }, nameof(AddNewChild));

    public Task AddChildren(List<int> memberId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            if (Family is null)
                return;

            var createResponse = await familiesHttpClient.AddChild(Family.FamilyId, new AddChildRequest(memberId), cancellationToken);
            if (createResponse is { Success: true })
            {
                await LoadFamily(Family.FamilyId);
                OnFamilyChanged?.Invoke();
            }
        }, nameof(AddChildren));

    public Task UpdateOwnedChild(int memberId, UpdateOwnedChildRequest request)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            if (Family is null)
                return;

            var response = await familiesHttpClient.UpdateOwnedChild(Family.FamilyId, memberId, request, cancellationToken);
            if (response is { Success: true })
            {
                await LoadFamily(Family.FamilyId);
                OnFamilyChanged?.Invoke();
            }
        }, nameof(UpdateOwnedChild));

    public Task RemoveFamilyMember(int memberId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            if (Family is null)
                return;

            var createResponse = await familiesHttpClient.DeleteFamilyMember(Family.FamilyId, memberId, cancellationToken);
            if (createResponse is { Success: true })
            {
                await LoadFamily(Family.FamilyId);
                OnFamilyChanged?.Invoke();
            }
        }, nameof(RemoveFamilyMember));
}