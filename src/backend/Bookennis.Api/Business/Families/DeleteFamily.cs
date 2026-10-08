using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record DeleteFamily(int FamilyId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteFamily>
    {
        public async Task<Unit> Handle(DeleteFamily request, CancellationToken cancellationToken)
        {
            // Parents, children and family members are auto-included and cascade-deleted with the family
            var family = await context.Families.Where(f => f.Id == request.FamilyId).SingleRequiredAsync(cancellationToken);
            context.Remove(family);
            await context.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}