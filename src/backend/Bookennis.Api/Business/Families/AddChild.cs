using Bookennis.Api.Data;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record AddChild(int FamilyId, List<int> MemberIds) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AddChild>
    {
        public async Task<Unit> Handle(AddChild request, CancellationToken cancellationToken)
        {
            var family = await context.Families.Where(f => f.Id == request.FamilyId).SingleAsync(cancellationToken);

            var users = await (from member in context.ClubMembers
                               let hasOtherFamily = context.FamilyMembers.Any(fm => fm.MemberId == member.Id && fm.FamilyId != family.Id)
                               where request.MemberIds.Contains(member.Id)
                               select new
                               {
                                   Member = member,
                                   HasOtherFamily = hasOtherFamily
                               }).ToListAsync(cancellationToken);

            if (users.Count != request.MemberIds.Count)
                throw new EntityNotFoundException("One or more of the provided MemberIds was not found");

            if (users.Any(u => u.HasOtherFamily))
                throw new ArgumentException("One of the provided family members already has another family");

            family.AddChildren(users.Select(u => u.Member.Id).ToList());

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}