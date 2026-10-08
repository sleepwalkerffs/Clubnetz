namespace Bookennis.Shared.Controller.Members;

public record UpdateMemberBookingOptionsRequest(int[] AllowedSeasonIds, int BookingsPerWeek);