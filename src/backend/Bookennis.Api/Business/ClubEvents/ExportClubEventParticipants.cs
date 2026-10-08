using System.Text.RegularExpressions;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEvents;

public record ExportClubEventParticipants(int ClubId, int UserId, int ClubEventId) : IQuery<ExportClubEventParticipantsResult>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer, IUserLanguageAccessor languageAccessor) : IRequestHandler<ExportClubEventParticipants, ExportClubEventParticipantsResult>
    {
        public async Task<ExportClubEventParticipantsResult> Handle(ExportClubEventParticipants request, CancellationToken cancellationToken)
        {
            var clubEvent = await context.ClubEvents.AsNoTracking().GetEventWithDetails(request.ClubId, request.ClubEventId, cancellationToken);
            var members = await ClubEventMapper.LoadMemberInfos(context, clubEvent, cancellationToken);
            var dto = ClubEventMapper.ToDto(renderer, clubEvent, members, null, DateTimeOffset.UtcNow);

            var data = new ClubEventExcelExporter(languageAccessor.GetCultureInfo()).Export(dto);

            return new ExportClubEventParticipantsResult(data, $"{ToFileName(dto.Title)} {dto.StartDate:yyyy-MM-dd}.xlsx");
        }

        private static string ToFileName(string name)
        {
            var fileName = Regex.Replace(name, @"[^\w\-. ]", "", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
            return fileName.Length == 0 ? "event" : fileName;
        }
    }
}

public record ExportClubEventParticipantsResult(byte[] Data, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
