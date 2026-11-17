using System.Text.RegularExpressions;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Bookennis.Api.Infrastructure.Audit;

namespace Bookennis.Api.Business.SubscriptionPlans;

[AuditedRead]
public record ExportSubscriptionPlan(int ClubId, int SubscriptionPlanId) : IQuery<ExportSubscriptionPlanResult>
{
    public class Handler(AppDbContext context, IUserLanguageAccessor languageAccessor) : IRequestHandler<ExportSubscriptionPlan, ExportSubscriptionPlanResult>
    {
        public async Task<ExportSubscriptionPlanResult> Handle(ExportSubscriptionPlan request, CancellationToken cancellationToken)
        {
            var plan = await context.SubscriptionPlans.AsNoTracking().GetPlanWithDetails(request.ClubId, request.SubscriptionPlanId, cancellationToken);
            var dto = SubscriptionPlanMapper.ToDto(plan);

            // Dates as the club writes them
            var culture = (await context.GetClubRegion(request.ClubId, cancellationToken)).Culture(languageAccessor.GetCultureInfo());
            var data = new SubscriptionPlanExcelExporter(culture).Export(dto);

            return new ExportSubscriptionPlanResult(data, $"{ToFileName(dto.Name)}.xlsx");
        }

        private static string ToFileName(string name)
        {
            var fileName = Regex.Replace(name, @"[^\w\-. ]", "", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
            return fileName.Length == 0 ? "subscription-plan" : fileName;
        }
    }
}

public record ExportSubscriptionPlanResult(byte[] Data, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
