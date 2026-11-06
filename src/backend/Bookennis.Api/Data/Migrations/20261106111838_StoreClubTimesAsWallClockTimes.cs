using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <summary>
    /// The web client used to convert every time of day it sent to UTC with the offset of the day it was sent, and back with the
    /// offset of the day it was read. So times saved in summer time were shown an hour early in winter and vice versa.
    /// The times of day are now wall-clock times of the club. This converts the stored values back to the local time the user
    /// entered, with the offset of Europe/Vienna on the day they were saved:
    /// - opening hours and prime time of a club: the last change of the club (the settings dialogs always send them),
    /// - start and end time of a club event: its creation (a later change may be the reminder flag, which the client did not send).
    /// Plain wall-clock values set by the backend are not touched: recurring booking series, court blockings, price rules.
    /// </summary>
    public partial class StoreClubTimesAsWallClockTimes : Migration
    {
        private const string ClubOffset =
            "((coalesce(\"Metadata_Modified\", \"Metadata_Created\") AT TIME ZONE 'Europe/Vienna') - (coalesce(\"Metadata_Modified\", \"Metadata_Created\") AT TIME ZONE 'UTC'))";

        private const string EventOffset =
            "((\"Metadata_Created\" AT TIME ZONE 'Europe/Vienna') - (\"Metadata_Created\" AT TIME ZONE 'UTC'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Shift(migrationBuilder, "+");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Shift(migrationBuilder, "-");

        // time ± interval wraps around midnight in PostgreSQL, so e.g. 23:00 UTC + 1 hour becomes 00:00
        private static void Shift(MigrationBuilder migrationBuilder, string sign)
        {
            migrationBuilder.Sql($"""
                UPDATE "Clubs" SET
                    "OpeningHours_From" = "OpeningHours_From" {sign} {ClubOffset},
                    "OpeningHours_To" = "OpeningHours_To" {sign} {ClubOffset},
                    "PrimeTimeSettings_PrimeTimeHours_From" = "PrimeTimeSettings_PrimeTimeHours_From" {sign} {ClubOffset},
                    "PrimeTimeSettings_PrimeTimeHours_To" = "PrimeTimeSettings_PrimeTimeHours_To" {sign} {ClubOffset};
                """);

            migrationBuilder.Sql($"""
                UPDATE "ClubEvents" SET
                    "StartTime" = "StartTime" {sign} {EventOffset},
                    "EndTime" = "EndTime" {sign} {EventOffset}
                WHERE "StartTime" IS NOT NULL;
                """);
        }
    }
}
