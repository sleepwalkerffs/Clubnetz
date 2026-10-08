using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Families;

namespace Bookennis.Client.Services.HttpClients.Family;

public class FamiliesHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IFamiliesHttpClient
{
    public async Task<HttpResult<GetFamilyResult>> GetFamily(int familyId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"{familyId}", cancellationToken)).AsHttpResult<GetFamilyResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetAvailableFamilyMembersResult>> GetAvailableFamilyMembers(int memberId, CancellationToken cancellationToken)
        => await (await httpClient.GetAsync($"AvailableFamilyMembers/{memberId}", cancellationToken)).AsHttpResult<GetAvailableFamilyMembersResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<int>> CreateFamily(CreateFamilyRequest model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync((string?)null, model, jsonOptions, cancellationToken)).AsHttpResult<int>(jsonOptions, cancellationToken);

    public async Task<HttpResult> AddParent(int familyId, AddParentRequest model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync($"{familyId}/Parent", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> AddChild(int familyId, AddChildRequest model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync($"{familyId}/Child", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> AddNewChild(int familyId, AddNewChildRequest model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync($"{familyId}/NewChild", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateOwnedChild(int familyId, int memberId, UpdateOwnedChildRequest model, CancellationToken cancellationToken)
        => await (await httpClient.PutAsJsonAsync($"{familyId}/Child/{memberId}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteFamily(int familyId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"{familyId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeleteFamilyMember(int familyId, int memberId, CancellationToken cancellationToken)
        => await (await httpClient.DeleteAsync($"{familyId}/Member/{memberId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}