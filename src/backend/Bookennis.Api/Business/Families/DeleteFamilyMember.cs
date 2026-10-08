using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record DeleteFamilyMember(int FamilyId, int MemberId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteFamilyMember>
    {
        public async Task<Unit> Handle(DeleteFamilyMember request, CancellationToken cancellationToken)
        {
            var family = await context.Families.Where(f => f.Id == request.FamilyId).SingleAsync(cancellationToken);

            family.DeleteFamilyMember(request.MemberId);

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}