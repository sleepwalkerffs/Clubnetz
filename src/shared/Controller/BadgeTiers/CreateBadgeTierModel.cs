using System.ComponentModel.DataAnnotations;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.BadgeTiers;

public record CreateBadgeTierModel(
    [Required] string Name,
    [Required] string Description,
    [Required] [Range(1, int.MaxValue)] int MatchesRequired,
    Sport? Sport = null);
