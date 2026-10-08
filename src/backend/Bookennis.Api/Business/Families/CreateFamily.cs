using Bookennis.Api.Business.Families.Helpers;
using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Domain.Families;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record CreateFamily(List<int> ParentContractUserIds, List<int> ChildrenUserContractIds, List<ChildUserModel> NewChildUsers, int UserId) : ICommand<int>
{
    public class Handler(AppDbContext context, ITenantService tenantService) : IRequestHandler<CreateFamily, int>
    {
        public async Task<int> Handle(CreateFamily request, CancellationToken cancellationToken)
        {
            var newChildMemberIds = await FamilyCreateUpdateHelper.AddNewChildUsers(context, tenantService.GetTenantId()!.Value, request.NewChildUsers, request.UserId, cancellationToken);
            request.ChildrenUserContractIds.AddRange(newChildMemberIds);

            var memberIds = request.ParentContractUserIds.Union(request.ChildrenUserContractIds).Distinct().ToList();
            if (memberIds.Count != request.ParentContractUserIds.Count + request.ChildrenUserContractIds.Count)
                throw new ArgumentException("Parent and Children can't contain the same Ids");

            var users = await (from member in context.ClubMembers
                               let hasFamily = context.FamilyMembers.Any(fm => fm.MemberId == member.Id)
                               where memberIds.Contains(member.Id)
                               select new
                               {
                                   IsParent = request.ParentContractUserIds.Contains(member.Id),
                                   Member = member,
                                   HasFamily = hasFamily
                               }).ToListAsync(cancellationToken);

            if (users.Count != memberIds.Count)
                throw new EntityNotFoundException("One of the provided Members does not exist");

            if (users.Any(u => u.HasFamily))
                throw new ArgumentException("One of the provided family members already has a family");

            var parents = users.Where(u => u.IsParent).Select(u => u.Member).ToList();
            var children = users.Where(u => !u.IsParent).Select(u => u.Member).ToList();

            var family = new Family(tenantService.GetTenantId()!.Value, parents.Select(i => i.Id).ToList(), children.Select(i => i.Id).ToList());

            context.Add(family);
            await context.SaveChangesAsync(cancellationToken);

            return family.Id;
        }
    }
}