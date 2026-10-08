using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Guests;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Guests;

public record GetGuestCards : IQuery<GetGuestCardResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetGuestCards, GetGuestCardResult>
    {
        public async Task<GetGuestCardResult> Handle(GetGuestCards request, CancellationToken cancellationToken)
        {
            var guestCards = await (from card in context.GuestCards
                                    join member in context.GuestMembers on card.GuestMemberId equals member.Id
                                    join user in context.Users on member.UserId equals user.Id
                                    orderby card.Metadata.Created descending
                                    select new GuestCardDto
                                    {
                                        Id = card.Id,
                                        Email = user.Email!,
                                        PurchasedBookings = card.PurchasedBookings
                                    }).ToListAsync(cancellationToken);

            return new GetGuestCardResult { GuestCards = guestCards };
        }
    }
}
