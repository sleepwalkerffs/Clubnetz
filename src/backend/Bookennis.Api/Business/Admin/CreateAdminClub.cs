using System.Drawing;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;

namespace Bookennis.Api.Business.Admin;

public record CreateAdminClub(string Name, TimeOnlyInterval OpeningHours, TimeOnlyInterval PrimeTimeHours, int? BookingGracePeriodInMinutes) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<CreateAdminClub, int>
    {
        public async Task<int> Handle(CreateAdminClub request, CancellationToken cancellationToken)
        {
            var club = new Club(
                request.Name,
                request.OpeningHours,
                request.PrimeTimeHours,
                [new Club.PlayModeDto(
                    [MemberRole.User],
                    Color.Green,
                    2,
                    false,
                    "Default",
                    TimeSpan.FromHours(1),
                    false,
                    false)],
                request.BookingGracePeriodInMinutes
            );

            context.Clubs.Add(club);
            await context.SaveChangesAsync(cancellationToken);
            return club.Id;
        }
    }
}
