using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Families;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record GetAvailableFamilyMembers(int MemberId) : ICommand<GetAvailableFamilyMembersResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAvailableFamilyMembers, GetAvailableFamilyMembersResult>
    {
        public async Task<GetAvailableFamilyMembersResult> Handle(GetAvailableFamilyMembers request, CancellationToken cancellationToken)
        {
            var availableFamilies = await (from member in context.ClubMembers
                                           join user in context.Users on member.UserId equals user.Id
                                           let hasFamily = context.FamilyMembers.Any(f => f.MemberId == member.Id)
                                           where member.Id != request.MemberId
                                           where !hasFamily
                                           select new FamilyMemberDto
                                           {
                                               FirstName = user.FirstName,
                                               LastName = user.LastName,
                                               MemberId = member.Id
                                           }).ToListAsync(cancellationToken);

            return new GetAvailableFamilyMembersResult { AvailableMembers = availableFamilies };
        }
    }
}