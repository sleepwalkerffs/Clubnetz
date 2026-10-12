namespace Bookennis.Api.Infrastructure.Authorization.Models;

/// <param name="FamilyId">The family that is read or changed.</param>
/// <param name="LeavingMemberId">Set when a member is removed from the family: members may take themselves out, also when they are no parent.</param>
public record FamilyAuthorizationModel(int FamilyId, int? LeavingMemberId = null);
