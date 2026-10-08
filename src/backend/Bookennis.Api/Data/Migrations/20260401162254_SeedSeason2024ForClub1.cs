using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSeason2024ForClub1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- 1. Insert the 2024 season (Apr 4 – Nov 1) for Club 1
                INSERT INTO "Seasons" ("Id", "ClubId", "Period_From", "Period_To", "Metadata_Created")
                SELECT nextval('"Seasons_Id_Sequence"'), 1, '2024-04-04', '2024-11-01', NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Seasons"
                    WHERE "ClubId" = 1 AND "Period_From" = '2024-04-04' AND "Period_To" = '2024-11-01'
                );

                -- 2. Link every member of Club 1 who has at least one booking within the season timeframe
                INSERT INTO "MemberSeasons" ("Id", "MemberId", "SeasonId", "Metadata_Created")
                SELECT nextval('"MemberSeasons_Id_Sequence"'), m."Id", s."Id", NOW()
                FROM "Members" m
                INNER JOIN "Seasons" s ON s."ClubId" = 1
                                      AND s."Period_From" = '2024-04-04'
                                      AND s."Period_To" = '2024-11-01'
                WHERE m."ClubId" = 1
                  AND EXISTS (
                      SELECT 1
                      FROM "BookingPlayers" bp
                      INNER JOIN "Bookings" b ON b."Id" = bp."BookingEntryId"
                      WHERE bp."MemberId" = m."Id"
                        AND b."ClubId" = 1
                        AND b."Interval_From" >= '2024-04-04T00:00:00Z'
                        AND b."Interval_From" < '2024-11-02T00:00:00Z'
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM "MemberSeasons" ms
                      WHERE ms."MemberId" = m."Id" AND ms."SeasonId" = s."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Remove MemberSeason links for the 2024 season of Club 1
                DELETE FROM "MemberSeasons"
                WHERE "SeasonId" IN (
                    SELECT "Id" FROM "Seasons"
                    WHERE "ClubId" = 1 AND "Period_From" = '2024-04-04' AND "Period_To" = '2024-11-01'
                );

                -- Remove the 2024 season for Club 1
                DELETE FROM "Seasons"
                WHERE "ClubId" = 1 AND "Period_From" = '2024-04-04' AND "Period_To" = '2024-11-01';
                """);
        }
    }
}
