using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Families;

namespace Bookennis.Client.Services.Store.Family;

public interface IFamilyStore : ISemaphoreStore
{
    public event Action? OnFamilyChanged;
    public FamilyDto? Family { get; }
    public List<FamilyMemberDto> AvailableFamilyMembers { get; }
    public Task LoadFamily(int familyId);
    public Task LoadAvailableFamilyMembers(int memberId);
    public Task<HttpResult> CreateFamily(CreateFamilyRequest request);
    public bool LoadedForFamilyId(int familyId);
    public void ResetFamily();
    public Task<HttpResult> AddParent(int memberId);
    public Task<HttpResult> AddNewChild(AddNewChildRequest request);
    public Task<HttpResult> AddChildren(List<int> memberId);
    public Task<HttpResult> UpdateOwnedChild(int memberId, UpdateOwnedChildRequest request);
    public Task<HttpResult> RemoveFamilyMember(int memberId);
}