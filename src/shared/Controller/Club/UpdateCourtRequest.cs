using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Club;

public record UpdateCourtRequest(string Name, string Alias, DateTimeOffsetInterval? Interval, int SortOrder);
