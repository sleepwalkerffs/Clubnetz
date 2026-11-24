using System.Reflection;
using Bookennis.Api.Infrastructure.Audit;
using Fusonic.Extensions.AspNetCore.Validation;
using Fusonic.Extensions.Hangfire;
using SimpleInjector;

namespace Bookennis.Api.Infrastructure.Configuration.ContainerRegistrations;

public static class MediatorConfiguration
{
    public static void AddMediator(this Container container, IServiceCollection services)
    {
        // The public demo only mails demo addresses (does nothing outside the demo)
        container.RegisterDecorator<IRequestHandler<Fusonic.Extensions.Email.SendEmail, Unit>, Business.Demo.DemoEmailGuard>();
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestValidationDecorator<,>));
        container.RegisterOutOfBandDecorators();
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(TransactionalRequestHandlerDecorator<,>));
        container.RegisterDecorator(typeof(INotificationHandler<>), typeof(TransactionalNotificationHandlerDecorator<>));
        // Registered last, so it is the outermost: it sees the outcome after the transaction and writes outside of it
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(AuditRequestHandlerDecorator<,>));

        container.RegisterMediator(services, [Assembly.GetExecutingAssembly()]);
    }
}