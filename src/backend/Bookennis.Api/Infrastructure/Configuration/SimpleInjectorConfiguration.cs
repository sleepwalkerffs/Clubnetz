using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Configuration.ContainerRegistrations;
using Bookennis.Api.Infrastructure.Tenant;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.Email;
using Microsoft.AspNetCore.Components.Web;
using SimpleInjector;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class SimpleInjectorConfiguration
{
    public static void AddSimpleInjector(this WebApplicationBuilder builder, Container container, AppSettings appSettings)
    {
        var services = builder.Services;
        services.AddSimpleInjector(
            container,
            options =>
            {
                options.AddAspNetCore()
                       .AddControllerActivation()
                       .AddViewComponentActivation();

                options.AddLogging();
                options.AutoCrossWireFrameworkComponents = true;

                container.AddMediator(services);
                container.AddServices();
                container.RegisterInstance(appSettings);

                var configuration = builder.Configuration;
                services.AddScoped<HtmlRenderer>();
                container.RegisterEmail(o => configuration.GetSection("Email").Bind(o));
                container.Collection.Append<IEmailAttachmentResolver, ClubAnnouncementAttachmentResolver>();

                // Manual CrossWire
                services.AddScoped(_ => container.GetInstance<IDomainEventDispatcher>());
                services.AddScoped(_ => container.GetInstance<IUserAccessor>());
                services.AddScoped(_ => container.GetInstance<ITenantService>());
                services.AddScoped(_ => container.GetInstance<IMediator>());
            }
        );
    }
}