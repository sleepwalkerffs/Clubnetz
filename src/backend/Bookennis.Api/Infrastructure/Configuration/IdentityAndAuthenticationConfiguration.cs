using System.Net;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Domain.User;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class IdentityAndAuthenticationConfiguration
{
    public static void AddIdentityAndAuthentication(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddIdentity<Domain.User.User, UserRole>(options =>
                 {
                     //most of the standard stuff is set to true so we leave it at that:
                     //Uppercase, lowercase, special char, digit and so on
                     options.Password.RequiredLength = 8;
                     options.User.RequireUniqueEmail = true;
                     options.SignIn.RequireConfirmedEmail = true;

                     // Brute force protection: after 5 wrong passwords (login or re-authentication) the account is locked for 15 minutes
                     options.Lockout.AllowedForNewUsers = true;
                     options.Lockout.MaxFailedAccessAttempts = 5;
                     options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                 })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>()
                .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CustomAuthenticationSchemes.Cookie;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(5);
            options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
            options.SlidingExpiration = true;

            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;

                    return Task.CompletedTask;
                }
            };
        });
    }
}