namespace Bookennis.Api.Infrastructure.Authorization.Models;

/// <param name="CourtPurchaseId">The purchase of the club the request is for.</param>
/// <param name="BuyerOnly">Only the buyer (and application administrators), not the treasurers and admins of the club: releasing a hold.</param>
public record CourtPurchaseAuthorizationModel(int CourtPurchaseId, bool BuyerOnly = false);
