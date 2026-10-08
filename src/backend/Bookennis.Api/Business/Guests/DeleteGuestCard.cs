using Bookennis.Api.Data;
using Fusonic.Extensions.EntityFrameworkCore;

namespace Bookennis.Api.Business.Guests;

public record DeleteGuestCard(int Id) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteGuestCard>
    {
        public async Task<Unit> Handle(DeleteGuestCard request, CancellationToken cancellationToken)
        {
            var guestCard = await context.GuestCards.SingleRequiredAsync(request.Id, cancellationToken);

            context.GuestCards.Remove(guestCard);
            await context.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
