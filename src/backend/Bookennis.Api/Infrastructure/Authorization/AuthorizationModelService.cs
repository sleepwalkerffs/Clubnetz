using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Authorization;

public class AuthorizationModelService(AppDbContext context)
{
    public async Task<int[]> GetClubIds(AuthorizationHandlerContext authContext)
        => authContext.Resource switch
        {
            BookingEntryAuthorizationModel bookingEntryModel => await GetClubIds(bookingEntryModel),
            PlayModeAuthorizationModel playModeAuthorizationModel => await GetClubIds(playModeAuthorizationModel),
            _ => throw new NotSupportedException("Unsupported resource type")
        };

    public async Task<int[]> GetPlayerIds(AuthorizationHandlerContext authContext)
        => authContext.Resource switch
        {
            BookingEntryAuthorizationModel bookingEntryModel => await GetPlayerIds(bookingEntryModel),
            _ => throw new NotSupportedException("Unsupported resource type")
        };

    public int GetPlayModeId(AuthorizationHandlerContext authContext)
        => authContext.Resource switch
        {
            PlayModeAuthorizationModel playModeAuthorizationModel => playModeAuthorizationModel.PlayModeId,
            _ => throw new NotSupportedException("Unsupported resource type")
        };

    private async Task<int[]> GetClubIds(PlayModeAuthorizationModel playModeAuthorizationModel)
        => await
            context.PlayModes
            .Where(entry => entry.Id == playModeAuthorizationModel.PlayModeId)
            .Select(entry => entry.ClubId)
            .ToArrayAsync();

    private async Task<int[]> GetClubIds(BookingEntryAuthorizationModel bookingEntryModel)
        => await
            context.Bookings.Where(entry => entry.Id == bookingEntryModel.BookingEntryId)
            .Select(entry => entry.ClubId)
            .ToArrayAsync();

    public async Task<bool> IsBookingInPast(AuthorizationHandlerContext authContext)
        => authContext.Resource switch
        {
            BookingEntryAuthorizationModel bookingEntryModel => await IsBookingInPast(bookingEntryModel),
            _ => throw new NotSupportedException("Unsupported resource type")
        };

    private async Task<int[]> GetPlayerIds(BookingEntryAuthorizationModel bookingEntryModel)
        => await context.BookingPlayers
            .Where(entry => entry.BookingEntryId == bookingEntryModel.BookingEntryId)
            .Select(entry => entry.MemberId)
            .ToArrayAsync();

    private async Task<bool> IsBookingInPast(BookingEntryAuthorizationModel bookingEntryModel)
        => await context.Bookings
            .Where(entry => entry.Id == bookingEntryModel.BookingEntryId)
            .Select(entry => entry.Interval.To < DateTimeOffset.UtcNow)
            .SingleAsync();
}
