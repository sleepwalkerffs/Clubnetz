using Bookennis.Api.Business.Families.Helpers;
using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record AddNewChild(int FamilyId, ChildUserModel ChildUserModel, int UserId) : ICommand
{
    public class Handler(AppDbContext context, ITenantService tenantService) : IRequestHandler<AddNewChild>
    {
        public async Task<Unit> Handle(AddNewChild request, CancellationToken cancellationToken)
        {
            var family = await context.Families.Where(f => f.Id == request.FamilyId).SingleAsync(cancellationToken);
            var newChildMemberId = (await FamilyCreateUpdateHelper.AddNewChildUsers(
                                        context, tenantService.GetTenantId()!.Value,
                                        newChildUsers: [request.ChildUserModel],
                                        request.UserId,
                                        cancellationToken)).Single();

            var hasFamilyAlready = await (from member in context.ClubMembers
                                          let hasOtherFamily = context.FamilyMembers.Any(fm => fm.MemberId == member.Id && fm.FamilyId != family.Id)
                                          where member.Id == newChildMemberId
                                          select hasOtherFamily).SingleAsync(cancellationToken);

            if (hasFamilyAlready)
                throw new ArgumentException("The provided family members already has another family");

            family.AddChildren([newChildMemberId]);

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}