using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;

namespace Bookennis.Api.Business.Notifications;

/// <summary>
/// Sends one of the club event emails (<see cref="ClubEmailType.ClubEventCreated"/>,
/// <see cref="ClubEmailType.ClubEventReminder"/>, <see cref="ClubEmailType.ClubEventRegistrationDeadline"/>)
/// to one batch of recipients. Use <see cref="IClubEventNotifier"/>, it resolves the recipients and creates the batches.
/// </summary>
public record SendClubEventEmailBatch(int ClubId, int ClubEventId, ClubEmailType Type, EmailRecipient[] Recipients) : ICommand
{
    // Runs as a background job: rendering and queueing the emails of a large club must not block the request
    [OutOfBand]
    public class Handler(AppDbContext context, IMediator mediator, AppSettings appSettings) : IRequestHandler<SendClubEventEmailBatch>
    {
        public async Task<Unit> Handle(SendClubEventEmailBatch request, CancellationToken cancellationToken)
        {
            // Background jobs have no tenant, the club is part of the request
            var clubEvent = await ClubEventInfo.Get(context, request.ClubEventId, request.ClubId, cancellationToken);

            // Deleted in the meantime
            if (clubEvent is null)
                return default;

            foreach (var recipient in request.Recipients)
            {
                var member = new MemberVariables(recipient.FirstName, recipient.LastName);
                var variables = new ClubEventEmailVariables(member, clubEvent.ToVariables(recipient.Language.ToCultureInfo(), appSettings));

                await mediator.Send(
                    new SendClubEmail(request.ClubId, request.Type, recipient.Email, member.FullName, recipient.Language, variables),
                    cancellationToken);
            }

            return default;
        }
    }
}
