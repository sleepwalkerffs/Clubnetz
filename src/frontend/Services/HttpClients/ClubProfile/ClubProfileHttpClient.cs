using System.Text.Json;
using Bookennis.Shared.Controller.ClubProfile;
using Microsoft.AspNetCore.Components;

namespace Bookennis.Client.Services.HttpClients.ClubProfile;

public class ClubProfileHttpClient(HttpClient httpClient, IServiceProvider serviceProvider, JsonSerializerOptions jsonOptions) : IClubProfileHttpClient
{
    public async Task<HttpResult<GetClubProfileResult>> GetClubProfile(int clubId, CancellationToken cancellationToken)
    {
        var apiUrl = serviceProvider.GetRequiredService<NavigationManager>().BaseApiUrl();
        //var apiUrl = configuration["ApiUrl"];
        return await (await httpClient.GetAsync(apiUrl! + $"Clubs/{clubId}/ClubProfile", cancellationToken)).AsHttpResult<GetClubProfileResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<GetBookingOptionsResult>> GetBookingOptions(CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("BookingOptions", cancellationToken)).AsHttpResult<GetBookingOptionsResult>(jsonOptions, cancellationToken);
}