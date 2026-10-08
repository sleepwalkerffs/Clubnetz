using System.Reflection;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Fusonic.Extensions.AspNetCore;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class AuthorizationConfiguration
{
    public static void AddAuthorization(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddScoped<AuthorizationModelService>();
        services.AddScoped<AuthorizationHandlerService>();
        services.AddAll<IAuthorizationHandler>([Assembly.GetExecutingAssembly()]);

        services.AddAuthorizationBuilder()
                // Administrator Policy
                .AddPolicy(AuthorizationPolicies.ApplicationAdministrator, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole(nameof(UserRoles.Administrator)))
                // Application User Policy
                .AddPolicy(AuthorizationPolicies.ApplicationUser, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)]))
                // Account Owner Policy: guest links sign in the underlying user, but must not allow changing its credentials
                .AddPolicy(AuthorizationPolicies.AccountOwner, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .RequireAssertion(context => context.User.FindFirst(GuestSessionClaims.GuestSession)?.Value != "true"))
                // Club Administrator Policy
                .AddPolicy(AuthorizationPolicies.ClubAdministrator, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Admin] }))

                // Club Treasurer Policy
                .AddPolicy(AuthorizationPolicies.ClubTreasurer, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Treasurer, MemberRole.Admin] }))

                // One-Time Badge Manager Policy
                .AddPolicy(AuthorizationPolicies.OneTimeBadgeManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Admin] }))

                // Club Event Manager Policy
                .AddPolicy(AuthorizationPolicies.ClubEventManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Maintainer, MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Admin] }))

                // Club Announcement Manager Policy
                .AddPolicy(AuthorizationPolicies.ClubAnnouncementManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.SportsDirector, MemberRole.YouthSportsDirector, MemberRole.Admin] }))

                // Court Blocking Manager Policy
                .AddPolicy(AuthorizationPolicies.CourtBlockingManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Maintainer, MemberRole.SportsDirector, MemberRole.Admin] }))

                // Club User Policy
                .AddPolicy(AuthorizationPolicies.Member, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.User, MemberRole.Guest] }))

                .AddPolicy(AuthorizationPolicies.ClubMember, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.User] }))

               // Booking Policy
               .AddPolicy(AuthorizationPolicies.OwnsBooking, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new OwnsBookingRequirement()))
               .AddPolicy(AuthorizationPolicies.CanEditBooking, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new CanEditBookingRequirement()))
               // Subscription Planner Policy
               .AddPolicy(AuthorizationPolicies.OwnsSubscriptionPlan, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new OwnsSubscriptionPlanRequirement()))
               // PlayMode Policy
               .AddPolicy(AuthorizationPolicies.CanBookPlayMode, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new CanBookPlayModeRequirement()));

    }
}