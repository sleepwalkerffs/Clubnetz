using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.OneTimeBadges;

public record CreateOneTimeBadgeModel(
    [Required] string Name,
    [Required] string Description);
