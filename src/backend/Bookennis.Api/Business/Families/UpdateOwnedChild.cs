using Bookennis.Api.Data;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Families;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Families;

public record UpdateOwnedChild(int FamilyId, int MemberId, UpdateOwnedChildRequest Request) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateOwnedChild>
    {
        public async Task<Unit> Handle(UpdateOwnedChild request, CancellationToken cancellationToken)
        {
            var family = await context.Families
                .Include(f => f.Children)
                .Where(f => f.Id == request.FamilyId)
                .SingleRequiredAsync(cancellationToken);

            if (family.Children.All(c => c.MemberId != request.MemberId))
                throw new ArgumentException("The provided member is not a child in this family");

            var user = await (from member in context.ClubMembers
                              join u in context.Users on member.UserId equals u.Id
                              where member.Id == request.MemberId
                              select u).SingleRequiredAsync(cancellationToken);

            if (!user.IsBelongingToUser)
                throw new ArgumentException("The provided member is not an owned account");

            user.Update(
                request.Request.FirstName,
                request.Request.LastName,
                request.Request.Birthday,
                (Gender)request.Request.Gender,
                user.Language,
                user.Street,
                user.City,
                user.ZipCode,
                user.Country);

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
