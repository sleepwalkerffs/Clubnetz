using Bookennis.Shared.Controller.Families;

namespace Bookennis.Client.Services.HttpClients.Family;

public interface IFamiliesHttpClient
{
    public Task<HttpResult<GetFamilyResult>> GetFamily(int familyId, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetAvailableFamilyMembersResult>> GetAvailableFamilyMembers(int memberId, CancellationToken cancellationToken = default);
    public Task<HttpResult<int>> CreateFamily(CreateFamilyRequest model, CancellationToken cancellationToken = default);
    public Task<HttpResult> AddParent(int familyId, AddParentRequest model, CancellationToken cancellationToken = default);
    public Task<HttpResult> AddChild(int familyId, AddChildRequest model, CancellationToken cancellationToken = default);
    public Task<HttpResult> AddNewChild(int familyId, AddNewChildRequest model, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateOwnedChild(int familyId, int memberId, UpdateOwnedChildRequest model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteFamily(int familyId, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeleteFamilyMember(int familyId, int memberId, CancellationToken cancellationToken = default);
}