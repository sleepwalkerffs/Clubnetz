using Bookennis.Shared.Controller.ClubProfile;

namespace Bookennis.Client.Services.HttpClients.ClubProfile;

public interface IClubProfileHttpClient
{
    Task<HttpResult<GetClubProfileResult>> GetClubProfile(int clubId, CancellationToken cancellationToken = default);

    Task<HttpResult<GetBookingOptionsResult>> GetBookingOptions(CancellationToken cancellationToken = default);
}
