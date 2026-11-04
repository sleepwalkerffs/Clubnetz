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

            // One person is no family: the last member would be stuck in it without a way out (the editor does not list oneself)
            if (family.Parents.Count + family.Children.Count <= 1)
                context.Remove(family);

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}