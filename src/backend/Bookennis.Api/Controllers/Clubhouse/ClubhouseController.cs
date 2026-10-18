using Bookennis.Api.Business.Clubhouse;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Fusonic.Extensions.Common.Security;
using Bookennis.Shared.Controller.Clubhouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClubhousePaymentMethod = Bookennis.Domain.Clubhouse.ClubhousePaymentMethod;

namespace Bookennis.Api.Controllers.Clubhouse;

/// <summary>
/// The clubhouse: the host ("Wirt") keeps the menu and books what guests eat and drink onto their tabs, which they pay at the end.
/// The money goes to the host, Clubnetz only keeps the list. Signed-in users only, never with an API key. Members read their own tabs.
/// </summary>
public class ClubhouseController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    // ---------- menu ----------

    [HttpGet("items")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<List<ClubhouseItemDto>> GetClubhouseItems(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubhouseItems(clubId), cancellationToken);

    [HttpPost("items")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseItemDto> CreateClubhouseItem(int clubId, [FromBody] SaveClubhouseItemModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateClubhouseItem(clubId, model), cancellationToken);

    [HttpPut("items/{itemId:int}")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseItemDto> UpdateClubhouseItem(int clubId, int itemId, [FromBody] SaveClubhouseItemModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateClubhouseItem(clubId, itemId, model), cancellationToken);

    [HttpDelete("items/{itemId:int}")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task DeleteClubhouseItem(int clubId, int itemId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteClubhouseItem(clubId, itemId), cancellationToken);

    // ---------- tabs (host) ----------

    /// <summary>The start screen of the host: open tabs, members who play today, regulars and what was sold today.</summary>
    [HttpGet("board")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseBoardResult> GetClubhouseBoard(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubhouseBoard(clubId, DateTimeOffset.UtcNow), cancellationToken);

    /// <summary>All members of the club with name and picture, for the host's search.</summary>
    [HttpGet("guests")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<List<ClubhouseGuestDto>> GetClubhouseGuests(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubhouseGuests(clubId), cancellationToken);

    /// <summary>Opens a tab for a member (or returns the open one) or for a guest from outside with a name.</summary>
    [HttpPost("tabs")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseTabDto> OpenClubhouseTab(int clubId, [FromBody] OpenClubhouseTabModel model, CancellationToken cancellationToken)
        => mediator.Send(new OpenClubhouseTab(clubId, userAccessor.GetUserId(), model.MemberId, model.GuestName), cancellationToken);

    /// <summary>The paid tabs by the day they were paid (club time zone), newest first.</summary>
    [HttpGet("tabs")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<GetClubhouseTabsResult> GetClubhouseTabs(int clubId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
        => mediator.Send(new GetClubhouseTabs(clubId, from, to, DateTimeOffset.UtcNow), cancellationToken);

    [HttpGet("tabs/{tabId:int}")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseTabDto> GetClubhouseTab(int clubId, int tabId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubhouseTab(clubId, tabId), cancellationToken);

    /// <summary>Books a round onto an open tab.</summary>
    [HttpPost("tabs/{tabId:int}/items")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseTabDto> AddClubhouseTabItems(int clubId, int tabId, [FromBody] AddClubhouseTabItemsModel model, CancellationToken cancellationToken)
        => mediator.Send(new AddClubhouseTabItems(clubId, userAccessor.GetUserId(), tabId, model.Items), cancellationToken);

    [HttpDelete("tabs/{tabId:int}/lines/{lineId:int}")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseTabDto> RemoveClubhouseTabLine(int clubId, int tabId, int lineId, CancellationToken cancellationToken)
        => mediator.Send(new RemoveClubhouseTabLine(clubId, tabId, lineId), cancellationToken);

    /// <summary>The guest paid the host: the tab is closed.</summary>
    [HttpPost("tabs/{tabId:int}/pay")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task<ClubhouseTabDto> PayClubhouseTab(int clubId, int tabId, [FromBody] PayClubhouseTabModel model, CancellationToken cancellationToken)
        => mediator.Send(new PayClubhouseTab(clubId, userAccessor.GetUserId(), tabId, (ClubhousePaymentMethod)model.PaymentMethod), cancellationToken);

    /// <summary>Discards an open tab with nothing on it.</summary>
    [HttpDelete("tabs/{tabId:int}")]
    [Authorize(AuthorizationPolicies.ClubhouseHost)]
    public Task DiscardClubhouseTab(int clubId, int tabId, CancellationToken cancellationToken)
        => mediator.Send(new DiscardClubhouseTab(clubId, tabId), cancellationToken);

    // ---------- members ----------

    /// <summary>The tabs of the caller and of the children of their families.</summary>
    [HttpGet("my-tabs")]
    [Authorize(AuthorizationPolicies.ClubhouseGuest)]
    public Task<MyClubhouseTabsResult> GetMyClubhouseTabs(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetMyClubhouseTabs(clubId, userAccessor.GetUserId()), cancellationToken);
}
