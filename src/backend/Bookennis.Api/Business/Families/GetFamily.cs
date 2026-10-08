using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Families;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record GetFamily(int FamilyId) : ICommand<GetFamilyResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetFamily, GetFamilyResult>
    {
        public async Task<GetFamilyResult> Handle(GetFamily request, CancellationToken cancellationToken)
        {
            var familyResult = await (from family in context.Families
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
                                                      select new FamilyChildDto
                                                      {
                                                          MemberId = child.MemberId,
                                                          FirstName = user.FirstName,
                                                          LastName = user.LastName,
                                                          IsOwnedAccount = user.IsBelongingToUser,
                                                          Birthday = user.Birthday,
                                                          Gender = (Gender)user.Gender
                                                      }).ToList()

                                      where family.Id == request.FamilyId

                                      select new GetFamilyResult
                                      {
                                          FamilyId = family.Id,
                                          Children = children,
                                          Parents = parents
                                      }).SingleRequiredAsync(cancellationToken);

            return familyResult;
        }
    }
}