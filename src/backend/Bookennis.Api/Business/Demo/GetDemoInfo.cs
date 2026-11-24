using Bookennis.Api.Config;
using Bookennis.Shared.Controller.Demo;

namespace Bookennis.Api.Business.Demo;

/// <summary>For the login page and the banner of the demo. Outside the demo it says so and nothing else.</summary>
public record GetDemoInfo : IQuery<GetDemoInfoResult>
{
    public class Handler(AppSettings appSettings) : IRequestHandler<GetDemoInfo, GetDemoInfoResult>
    {
        public Task<GetDemoInfoResult> Handle(GetDemoInfo request, CancellationToken cancellationToken)
        {
            if (!appSettings.Demo.Enabled)
                return Task.FromResult(new GetDemoInfoResult());

            var twoClubs = DemoContent.Europe.Personas.Single(p => p.Persona == DemoPersona.TwoClubs);
            return Task.FromResult(new GetDemoInfoResult
            {
                IsEnabled = true,
                Password = DemoAccounts.Password,
                Clubs = DemoContent.Clubs.Select(club => new DemoClubDto
                {
                    Name = club.Name,
                    Region = club.Key,
                    Accounts = club.Personas.Append(twoClubs).DistinctBy(p => p.Email).Select(p => new DemoAccountDto
                    {
                        Persona = p.Persona!.Value.ToString(),
                        Name = $"{p.FirstName} {p.LastName}",
                        Email = p.Email,
                    }).ToList(),
                }).ToList(),
            });
        }
    }
}
