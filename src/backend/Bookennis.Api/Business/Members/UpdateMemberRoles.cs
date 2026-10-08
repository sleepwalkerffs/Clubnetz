using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

public record UpdateMemberRoles(int MemberId, MemberRole[] Roles) : ICommand
{
    public class Handler(AppDbContext context) : AsyncRequestHandler<UpdateMemberRoles>
    {
        protected override async Task Handle(UpdateMemberRoles request, CancellationToken cancellationToken)
        {
            var member = await context.ClubMembers.SingleRequiredAsync(m => m.Id == request.MemberId, cancellationToken);

            member.UpdateRoles(request.Roles);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
