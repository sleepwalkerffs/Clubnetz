using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Club;

public record AddCourtRequest(string Name, string Alias, bool IsIndoor = false, Sport Sport = Sport.Tennis);
