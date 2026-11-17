using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Members;

/// <summary>
/// <c>SeasonSports</c>: the sports of the enrollments (empty sports = every sport). A season that is not listed keeps its sports,
/// a new enrollment without an entry is for every sport.
/// </summary>
public record UpdateMemberBookingOptionsRequest(int[] AllowedSeasonIds, int BookingsPerWeek, List<SeasonSports>? SeasonSports = null);

public record SeasonSports(int SeasonId, List<Sport> Sports);
