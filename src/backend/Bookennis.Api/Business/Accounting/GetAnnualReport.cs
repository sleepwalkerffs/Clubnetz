using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Accounting;

/// <summary>The annual report of a fiscal year for the general meeting as a PDF, in the language of the request.</summary>
[AuditedRead]
public record GetAnnualReport(int ClubId, int FiscalYearId, DateTimeOffset Now) : IQuery<AnnualReportFile>
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor, IMediator mediator) : IRequestHandler<GetAnnualReport, AnnualReportFile>
    {
        public async Task<AnnualReportFile> Handle(GetAnnualReport request, CancellationToken cancellationToken)
        {
            var year = await context.GetFiscalYear(request.ClubId, request.FiscalYearId, cancellationToken);
            var previous = await context.GetPreviousFiscalYear(year, cancellationToken);
            var statement = await mediator.Send(new GetIncomeStatement(request.ClubId, year.Id), cancellationToken);
            var summary = await mediator.Send(new GetAccountingSummary(request.ClubId, year.Id), cancellationToken);
            var names = await context.MemberNames(request.ClubId, [year.AuditedByMemberId], cancellationToken);

            var data = new AnnualReportPdfData(
                await context.Clubs.Where(c => c.Id == request.ClubId).Select(c => c.Name).SingleAsync(cancellationToken),
                year,
                statement.PreviousFiscalYearId is not null ? previous?.Label : null,
                year.AuditedByMemberId is { } auditor ? names.GetValueOrDefault(auditor) : null,
                statement,
                summary,
                await context.AccountingCategories.AsNoTracking().Where(c => c.ClubId == request.ClubId).ToDictionaryAsync(c => c.Id, cancellationToken),
                await context.MoneyAccounts.AsNoTracking().Where(a => a.ClubId == request.ClubId).ToDictionaryAsync(a => a.Id, cancellationToken),
                AccountingQueries.Today(request.Now));

            var pdf = new AnnualReportPdf(languageAccessor.GetCultureInfo());
            return new AnnualReportFile(pdf.Render(data), pdf.FileName(year));
        }
    }
}

public record AnnualReportFile(byte[] Data, string FileName)
{
    public const string ContentType = "application/pdf";
}
