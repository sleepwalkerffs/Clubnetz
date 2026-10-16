using Bookennis.Api.Data;

namespace Bookennis.Api.Business.Members;

/// <summary>The member of the user in the club and the children of the families they are a parent of (<see cref="ActiveMembership.OwnAndChildMemberIds"/>).</summary>
public record GetOwnAndChildMemberIds(int ClubId, int UserId) : IQuery<IReadOnlySet<int>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetOwnAndChildMemberIds, IReadOnlySet<int>>
    {
        public async Task<IReadOnlySet<int>> Handle(GetOwnAndChildMemberIds request, CancellationToken cancellationToken)
            => await ActiveMembership.OwnAndChildMemberIds(context, request.ClubId, request.UserId, cancellationToken);
    }
}
