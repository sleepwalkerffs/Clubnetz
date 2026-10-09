using Bookennis.Api.Config;
using Bookennis.Shared.Controller.Legal;

namespace Bookennis.Api.Business.Legal;

public record GetLegalSettings : IQuery<GetLegalSettingsResult>
{
    public class Handler(AppSettings appSettings) : IRequestHandler<GetLegalSettings, GetLegalSettingsResult>
    {
        public Task<GetLegalSettingsResult> Handle(GetLegalSettings request, CancellationToken cancellationToken)
        {
            var legal = appSettings.Legal;
            return Task.FromResult(new GetLegalSettingsResult
            {
                OperatorName = Trim(legal.OperatorName),
                Street = Trim(legal.Street),
                ZipCode = Trim(legal.ZipCode),
                City = Trim(legal.City),
                Country = Trim(legal.Country),
                Email = Trim(legal.Email),
                Phone = string.IsNullOrWhiteSpace(legal.Phone) ? null : legal.Phone.Trim(),
                HostingProvider = Trim(legal.HostingProvider),
                EmailProvider = Trim(legal.EmailProvider)
            });
        }

        private static string Trim(string? value) => value?.Trim() ?? "";
    }
}
