using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record AddParent(int FamilyId, int MemberId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AddParent>
    {
        public async Task<Unit> Handle(AddParent request, CancellationToken cancellationToken)
        {
            var family = await context.Families.Where(f => f.Id == request.FamilyId).SingleAsync(cancellationToken);

            var user = await (from member in context.ClubMembers
                              let hasOtherFamily = context.FamilyMembers.Any(fm => fm.MemberId == member.Id && fm.FamilyId != family.Id)
                              where member.Id == request.MemberId
                              select new
                              {
                                  Member = member,
                                  HasOtherFamily = hasOtherFamily
                              }).SingleAsync(cancellationToken);

            if (user.HasOtherFamily)
                throw new ArgumentException("The provided family members already has another family");

            family.AddParent(user.Member.Id);

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}