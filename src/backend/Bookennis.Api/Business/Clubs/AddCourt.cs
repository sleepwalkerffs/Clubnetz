using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

/// <summary>Adds a court to the current club. It is placed after the existing courts.</summary>
public record AddCourt(string Name, string Alias, bool IsIndoor = false, Sport Sport = Sport.Tennis) : ICommand<int>
{
    public class Handler(AppDbContext context) : IRequestHandler<AddCourt, int>
    {
        public async Task<int> Handle(AddCourt request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.SingleRequiredAsync(cancellationToken);
            var sortOrder = (await context.Courts.MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0) + 1;

            var court = new Court(club.Id, request.Name.Trim(), request.Alias.Trim(), sortOrder);
            court.SetIndoor(request.IsIndoor);
            court.SetSport(request.Sport);
            context.Courts.Add(court);

            await context.SaveChangesAsync(cancellationToken);
            return court.Id;
        }
    }
}
