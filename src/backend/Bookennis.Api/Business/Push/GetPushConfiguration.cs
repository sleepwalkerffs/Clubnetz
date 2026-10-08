using Bookennis.Api.Config;
using Bookennis.Shared.Controller.Push;

namespace Bookennis.Api.Business.Push;

public record GetPushConfiguration : IQuery<GetPushConfigurationResult>
{
    public class Handler(AppSettings appSettings) : IRequestHandler<GetPushConfiguration, GetPushConfigurationResult>
    {
        public Task<GetPushConfigurationResult> Handle(GetPushConfiguration request, CancellationToken cancellationToken)
            => Task.FromResult(new GetPushConfigurationResult
            {
                PublicKey = appSettings.Push.IsConfigured ? appSettings.Push.PublicKey : null
            });
    }
}
