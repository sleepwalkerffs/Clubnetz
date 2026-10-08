using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>Emails of all members matching the filter of the members list (not paged).</summary>
public record GetMemberEmails(MemberFilter Filter) : IQuery<GetMemberEmailsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMemberEmails, GetMemberEmailsResult>
    {
        public async Task<GetMemberEmailsResult> Handle(GetMemberEmails request, CancellationToken cancellationToken)
        {
            var members = await MemberFilterQuery.Build(context, request.Filter, cancellationToken);

            var emails = await members
                .Where(r => r.ContactEmail != null)
                .OrderBy(r => r.User.LastName)
                .ThenBy(r => r.User.FirstName)
                .Select(r => r.ContactEmail!)
                .ToListAsync(cancellationToken);

            // Siblings share the email of their parent, every address is only needed once
            return new GetMemberEmailsResult { Emails = emails.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
        }
    }
}
