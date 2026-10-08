using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Guests;

namespace Bookennis.Client.Services.Store.Guests;

public interface IGuestStore : ISemaphoreStore
{
    event Action? OnGuestsChanged;

    bool GuestCardsLoaded { get; }
    List<GuestCardDto> GuestCards { get; }

    Task LoadGuests();
    Task DeleteGuest(int id);
    Task<HttpResult> CreateGuestCard(CreateGuestCardRequest request);
}
