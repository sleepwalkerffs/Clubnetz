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
    public Task CreateFamily(CreateFamilyRequest request);
    public bool LoadedForFamilyId(int familyId);
    public void ResetFamily();
    public Task AddParent(int memberId);
    public Task AddNewChild(AddNewChildRequest request);
    public Task AddChildren(List<int> memberId);
    public Task UpdateOwnedChild(int memberId, UpdateOwnedChildRequest request);
    public Task RemoveFamilyMember(int memberId);
}