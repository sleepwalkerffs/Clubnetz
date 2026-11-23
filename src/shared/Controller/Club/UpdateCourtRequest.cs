using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Club;

public record UpdateCourtRequest(string Name, string Alias, DateTimeOffsetInterval? Interval, int SortOrder, bool? IsIndoor = null, Sport? Sport = null);
