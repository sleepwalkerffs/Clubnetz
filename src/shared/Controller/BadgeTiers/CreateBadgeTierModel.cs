using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.BadgeTiers;

public record CreateBadgeTierModel(
    [Required] string Name,
    [Required] string Description,
    [Required] [Range(1, int.MaxValue)] int MatchesRequired);
