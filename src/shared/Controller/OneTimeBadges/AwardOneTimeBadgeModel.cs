using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.OneTimeBadges;

public record AwardOneTimeBadgeModel(
    [Required] [MinLength(1)] List<int> MemberIds);
