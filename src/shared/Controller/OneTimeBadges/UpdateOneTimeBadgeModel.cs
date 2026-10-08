using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.OneTimeBadges;

public record UpdateOneTimeBadgeModel(
    [Required] string Name,
    [Required] string Description);
