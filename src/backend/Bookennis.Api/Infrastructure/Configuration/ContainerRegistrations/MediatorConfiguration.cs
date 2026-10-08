using System.Reflection;
using Fusonic.Extensions.AspNetCore.Validation;
using Fusonic.Extensions.Hangfire;
using SimpleInjector;

namespace Bookennis.Api.Infrastructure.Configuration.ContainerRegistrations;

public static class MediatorConfiguration
{
    public static void AddMediator(this Container container, IServiceCollection services)
    {
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestValidationDecorator<,>));
        container.RegisterOutOfBandDecorators();
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(TransactionalRequestHandlerDecorator<,>));
        container.RegisterDecorator(typeof(INotificationHandler<>), typeof(TransactionalNotificationHandlerDecorator<>));

        container.RegisterMediator(services, [Assembly.GetExecutingAssembly()]);
    }
}