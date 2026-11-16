namespace Bookennis.Shared.Controller.Club;

public record AddCourtRequest(string Name, string Alias, bool IsIndoor = false);
