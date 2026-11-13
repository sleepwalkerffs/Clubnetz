using System.Reflection;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Domain.Members;
using Bookennis.Domain.Permissions;
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
        services.AddScoped<ResourcePermissionService>();
        services.AddAll<IAuthorizationHandler>([Assembly.GetExecutingAssembly()]);
        // Logs the requests a policy forbids (audit log)
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, Audit.AuditingAuthorizationResultHandler>();

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
                // Account Owner Policy: the signed-in user changes the own credentials
                .AddPolicy(AuthorizationPolicies.AccountOwner, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)]))
                // Club Administrator Policy
                .AddPolicy(AuthorizationPolicies.ClubAdministrator, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Admin] }))

                // Club API Key Manager Policy: only a signed-in admin, never an API key, so a key can't create or revoke keys
                .AddPolicy(AuthorizationPolicies.ClubApiKeyManager, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .RequireAssertion(context => !context.User.IsClubApiKey())
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Admin] }))

                // Permission Manager Policy: only a signed-in admin, never an API key, so a key can't give itself or anybody more rights
                .AddPolicy(AuthorizationPolicies.PermissionManager, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .RequireAssertion(context => !context.User.IsClubApiKey())
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Admin] }))

                // Club Payment Account Manager Policy: money matters. Only a signed-in admin connects the Stripe account, never an API key and not a treasurer.
                .AddPolicy(AuthorizationPolicies.ClubPaymentAccountManager, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .RequireAssertion(context => !context.User.IsClubApiKey())
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.Admin] }))

                // Club Treasurer Policy: membership fees, plans (prices) and payments are money, so never open to a club API key
                .AddPolicy(AuthorizationPolicies.ClubTreasurer, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PermissionRequirement(Permission.PaymentsManage)))

                // A member ordering or paying a membership: cookie only like every other purchase
                .AddPolicy(AuthorizationPolicies.MembershipMember, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.User] }))

                // One-Time Badge Manager Policy
                .AddPolicy(AuthorizationPolicies.OneTimeBadgeManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new PermissionRequirement(Permission.OneTimeBadgesManage)))

                // Club Event Manager Policy
                .AddPolicy(AuthorizationPolicies.ClubEventManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new PermissionRequirement(Permission.EventsManage)))

                // Club Announcement Manager Policy
                .AddPolicy(AuthorizationPolicies.ClubAnnouncementManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new PermissionRequirement(Permission.AnnouncementsManage)))

                // Court Blocking Manager Policy
                .AddPolicy(AuthorizationPolicies.CourtBlockingManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new PermissionRequirement(Permission.CourtBlockingsManage)))

                // Club Member Viewer Policy
                .AddPolicy(AuthorizationPolicies.ClubMemberViewer, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new PermissionRequirement(Permission.MembersView)))

                // Club Statistics Viewer Policy
                .AddPolicy(AuthorizationPolicies.ClubStatisticsViewer, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new PermissionRequirement(Permission.StatisticsView)))

                // Active Member Policy: everything that shows other members
                .AddPolicy(AuthorizationPolicies.ActiveMember, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .AddRequirements(new ActiveMemberRequirement()))

                // Court Sales Policies: cookie only, so court sales (money, holds) can't be reached with an API key
                .AddPolicy(AuthorizationPolicies.CourtSalesMember, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [] }))
                .AddPolicy(AuthorizationPolicies.CourtSalesManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PermissionRequirement(Permission.CourtSalesManage)))
                .AddPolicy(AuthorizationPolicies.OwnsCourtPurchase, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new OwnsCourtPurchaseRequirement()))

                // Clubhouse Policies: cookie only, the tabs are about the money of the host
                .AddPolicy(AuthorizationPolicies.PaysClubhouseTab, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PaysClubhouseTabRequirement()))
                .AddPolicy(AuthorizationPolicies.ClubhouseGuest, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [] }))
                .AddPolicy(AuthorizationPolicies.ClubhouseHost, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PermissionRequirement(Permission.ClubhouseHost)))

                // Accounting policies: cookie only, the books of the club are never open to an API key
                .AddPolicy(AuthorizationPolicies.AccountingReader, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PermissionRequirement(Permission.AccountingRead)))
                .AddPolicy(AuthorizationPolicies.AccountingManager, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PermissionRequirement(Permission.AccountingManage)))
                .AddPolicy(AuthorizationPolicies.AccountingAuditor, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new PermissionRequirement(Permission.AccountingAudit)))

                // Play request policies: cookie only. Requests are personal (who plays with whom), so they are no business of API keys
                .AddPolicy(AuthorizationPolicies.PlayRequestMember, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [] }, new ActiveMemberRequirement()))
                .AddPolicy(AuthorizationPolicies.TakesPartInPlayRequest, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                          .RequireAuthenticatedUser()
                          .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                          .RequireAssertion(context => !context.User.IsClubApiKey())
                          .AddRequirements(new TakesPartInPlayRequestRequirement()))

                // Club User Policy
                .AddPolicy(AuthorizationPolicies.Member, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.User, MemberRole.Guest] }))

                .AddPolicy(AuthorizationPolicies.ClubMember, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [MemberRole.User] }))

                // Any Member Policy
                .AddPolicy(AuthorizationPolicies.AnyMember, policy =>
                     policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                           .RequireAuthenticatedUser()
                           .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                           .AddRequirements(new ClubAssignmentRequirement { LimitToMemberRoles = [] }))

               // Booking Policy
               .AddPolicy(AuthorizationPolicies.OwnsBooking, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new OwnsBookingRequirement()))
               .AddPolicy(AuthorizationPolicies.CanEditBooking, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new CanEditBookingRequirement()))
               // Family Policies
               .AddPolicy(AuthorizationPolicies.FamilyMember, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new FamilyAccessRequirement { ParentsOnly = false }))
               .AddPolicy(AuthorizationPolicies.FamilyParent, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new FamilyAccessRequirement { ParentsOnly = true }))
               // Membership Order Policy
               .AddPolicy(AuthorizationPolicies.OwnsMembershipOrder, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                            .RequireAuthenticatedUser()
                            .RequireAssertion(context => !context.User.IsClubApiKey())
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new OwnsMembershipOrderRequirement()))
               // Subscription Planner Policy
               .AddPolicy(AuthorizationPolicies.OwnsSubscriptionPlan, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.Cookie)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new OwnsSubscriptionPlanRequirement()))
               // PlayMode Policy
               .AddPolicy(AuthorizationPolicies.CanBookPlayMode, policy =>
                    policy.AddAuthenticationSchemes(CustomAuthenticationSchemes.CookieOrClubApiKey)
                            .RequireAuthenticatedUser()
                            .RequireRole([nameof(UserRoles.User), nameof(UserRoles.Administrator)])
                            .AddRequirements(new CanBookPlayModeRequirement()));

    }
}