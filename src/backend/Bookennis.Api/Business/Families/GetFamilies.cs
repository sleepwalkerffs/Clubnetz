using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Families;
using Bookennis.Shared.Controller.Shared;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record GetFamilies : ICommand<GetFamiliesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetFamilies, GetFamiliesResult>
    {
        public async Task<GetFamiliesResult> Handle(GetFamilies request, CancellationToken cancellationToken)
        {
            var families = await (from family in context.Families
                                  let parents = (from parent in family.Parents
                                                 join member in context.ClubMembers on parent.MemberId equals member.Id
                                                 join user in context.Users on member.UserId equals user.Id
                                                 select new FamilyParentDto
                                                 {
                                                     MemberId = parent.MemberId,
                                                     FirstName = user.FirstName,
                                                     LastName = user.LastName
                                                 }).ToList()

                                  let children = (from child in family.Children
                                                  join member in context.ClubMembers on child.MemberId equals member.Id
                                                  join user in context.Users on member.UserId equals user.Id
                                                  select new FamilyChildDto()
                                                  {
                                                      MemberId = child.MemberId,
                                                      FirstName = user.FirstName,
                                                      LastName = user.LastName,
                                                      IsOwnedAccount = user.IsBelongingToUser,
                                                      Birthday = user.Birthday,
                                                      Gender = (Gender)user.Gender
                                                  }).ToList()

                                  select new FamilyDto
                                  {
                                      FamilyId = family.Id,
                                      Children = children,
                                      Parents = parents
                                  }).ToListAsync(cancellationToken);

            return new GetFamiliesResult { Families = families };
        }
    }
}