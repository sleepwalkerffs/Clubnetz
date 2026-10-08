using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.Guests;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Guests;

namespace Bookennis.Client.Services.Store.Guests;

public class GuestStore(IGuestHttpClient guestHttpClient) : SemaphoreStore, IGuestStore
{
    public List<GuestCardDto> GuestCards { get; private set; } = [];

    public bool GuestCardsLoaded { get; private set; }

    public event Action? OnGuestsChanged;

    public Task LoadGuests() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var result = await guestHttpClient.GetGuestCards(cancellationToken);
        if (result is { Success: true, Dto: not null })
        {
            GuestCards = result.Dto.GuestCards;
            GuestCardsLoaded = true;
            OnGuestsChanged?.Invoke();
        }
    }, nameof(LoadGuests));

    public Task DeleteGuest(int id) => RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await guestHttpClient.DeleteGuestCard(id, cancellationToken);
        if (result.Success)
        {
            GuestCards.RemoveAll(x => x.Id == id);
            OnGuestsChanged?.Invoke();
        }
    }, nameof(DeleteGuest));

    public async Task<HttpResult> CreateGuestCard(CreateGuestCardRequest request) => await RunInSavingContextAsync(async cancellationToken =>
    {
        var result = await guestHttpClient.CreateGuestCard(request, cancellationToken);
        if (result is { Success: true, Dto: not null })
        {
            GuestCards.Insert(0, new GuestCardDto
            {
                Email = request.Email,
                Id = result.Dto.GuestCardId,
                PurchasedBookings = request.PurchasedBookings
            });

            OnGuestsChanged?.Invoke();
        }
        return result;

    }, nameof(CreateGuestCard));
}
