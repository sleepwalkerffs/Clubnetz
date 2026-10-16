using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Requirements;

/// <summary>The caller is an active member of the club (<see cref="Business.Members.ActiveMembership"/>), or an application administrator.</summary>
public class ActiveMemberRequirement : IAuthorizationRequirement;
