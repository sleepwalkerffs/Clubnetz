using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Api.Infrastructure.Configuration.Hangfire;
using Bookennis.Api.Infrastructure.Tenant;
using Fusonic.Extensions.AspNetCore.Http;
using Fusonic.Extensions.Email;
using Hangfire;
using Microsoft.Extensions.Options;
using SimpleInjector;
using SimpleInjector.Lifestyles;

#pragma warning disable CA1852

var builder = WebApplication.CreateBuilder(args);

var container = new Container();
container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
container.Options.DefaultLifestyle = Lifestyle.Scoped;

var services = builder.Services;

//AppSettings
var configuration = builder.Configuration;
services.Configure<AppSettings>(configuration);
var appSettings = configuration.Get<AppSettings>()!;
services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value);

//Use Services
builder.AddSerilog();
builder.AddDatabase(appSettings);
builder.AddIdentityAndAuthentication();
builder.AddAuthorization();
builder.AddControllerRouting();
builder.AddHangfire(appSettings, container);
builder.AddSimpleInjector(container, appSettings);
builder.AddSecurityHeaders();
builder.AddRateLimiting();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();
SimpleInjectorUseOptionsAspNetCoreExtensions.UseSimpleInjector(app, container);

// Must run first so the headers are applied to every response, including ones later
// middleware (e.g. static files) may short-circuit. Also covers HSTS for HTTPS
// responses, so the app doesn't need its own app.UseHsts() call.
app.UseSecurityHeaders();

// Resolves the client IP from X-Forwarded-For (Azure App Service), needed for the per-IP rate limits
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();

    // The API description is only exposed locally, not in production
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint(url: "/swagger/v1/swagger.json", name: "Tennis API"));
}

//maybe this: https://github.com/dotnet/runtime/issues/23801
app.UseHttpsRedirection();

// Intercept BEFORE UseBlazorFrameworkFiles so non-fingerprinted assets always get
// no-cache — including index.html, which UseBlazorFrameworkFiles serves for "/" and
// which our later UseStaticFiles never gets a chance to touch.
// Using OnStarting ensures the header is written AFTER the file middleware runs,
// so it wins even if the file middleware also sets Cache-Control.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    // Fingerprinted _framework/* files are safe to cache forever — leave them alone.
    if (!path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-cache";
            return Task.CompletedTask;
        });
    }
    await next();
});

app.UseAppVersionHeader();

app.UseBlazorFrameworkFiles();

// Non-fingerprinted static assets (js-helpers.js, index.html, etc.) must revalidate on
// every request so browsers never serve stale code after a deployment.
// Blazor framework files (_framework/*) are excluded — they are content-addressed and
// already carry Cache-Control: max-age=31536000, immutable from UseBlazorFrameworkFiles().
var appStaticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
};

app.UseStaticFiles(appStaticFileOptions);

app.UseIgnorePaths("/swagger");

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<MultiTenantServiceMiddleware>(container);
app.UseAuthorization();
app.UseRateLimiter();

app.UseCors();

app.MapRazorPages();

app.MapControllers();

// Anonymous liveness/readiness probe for the hosting environment (checks the database connection)
app.MapHealthChecks("/health").AllowAnonymous();

//app.Map(
//    "api/{**slug}",
//    (HttpContext ctx) =>
//    {
//        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
//        return Task.CompletedTask;
//    }
//);

app.UseHangfireDashboard(options: new DashboardOptions
{
    Authorization = new[] { new AdminRoleAuthorizationFilter() }
});

app.MapFallbackToFile("{**slug}", "index.html", appStaticFileOptions);

// Reminders (push and email) for bookings and club events that start soon
var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<BookingReminderJob>(BookingReminderJob.JobId, job => job.Run(), BookingReminderJob.Schedule);
recurringJobs.AddOrUpdate<ClubEventReminderJob>(ClubEventReminderJob.JobId, job => job.Run(), ClubEventReminderJob.Schedule);

container.Verify();

app.Run();
