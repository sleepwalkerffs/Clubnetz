using Bookennis.Api.Data;
using Bookennis.Domain.Courts;

namespace Bookennis.Api.Business.Admin;

public record AdminAddCourt(int ClubId, string Name, string Alias, int SortOrder) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminAddCourt>
    {
        public async Task<Unit> Handle(AdminAddCourt request, CancellationToken cancellationToken)
        {
            var court = new Court(request.ClubId, request.Name, request.Alias, request.SortOrder);
            context.Courts.Add(court);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
