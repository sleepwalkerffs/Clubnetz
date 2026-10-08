using Bookennis.Domain.Members;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Requirements;

public record ClubAssignmentRequirement : IAuthorizationRequirement
{
    public required MemberRole[] LimitToMemberRoles { get; set; } = [];
}