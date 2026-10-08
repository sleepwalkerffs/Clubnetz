using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AwardBadgesForPreviousSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retroactively award badges to members for all past seasons.
            // For each member/season combination, count qualifying bookings and
            // insert any badge tiers they've reached that they don't already have.
            migrationBuilder.Sql("""
                INSERT INTO "MemberBadges" ("Id", "MemberId", "BadgeTierId", "SeasonId", "EarnedAt", "Metadata_Created")
                SELECT
                    nextval('"MemberBadges_Id_Sequence"'),
                    member_counts."MemberId",
                    bt."Id",
                    member_counts."SeasonId",
                    NOW(),
                    NOW()
                FROM (
                    SELECT
                        bp."MemberId",
                        s."Id"      AS "SeasonId",
                        m."ClubId",
                        COUNT(*)    AS "MatchCount"
                    FROM "BookingPlayers" bp
                    JOIN "Bookings"  b  ON bp."BookingEntryId" = b."Id"
                    JOIN "PlayModes" pm ON b."PlayModeId"      = pm."Id"
                    JOIN "Members"   m  ON bp."MemberId"       = m."Id"
                    JOIN "Seasons"   s  ON s."ClubId"          = m."ClubId"
                        AND (b."Interval_From" AT TIME ZONE 'UTC') >= s."Period_From"::timestamp
                        AND (b."Interval_To"   AT TIME ZONE 'UTC') <  s."Period_To"::timestamp + INTERVAL '1 day'
                    WHERE pm."IsChargingBookingSubscription" = true
                      AND b."Interval_To" < NOW()
                    GROUP BY bp."MemberId", s."Id", m."ClubId"
                ) member_counts
                JOIN "BadgeTiers" bt ON bt."ClubId" = member_counts."ClubId"
                    AND member_counts."MatchCount" >= bt."MatchesRequired"
                WHERE NOT EXISTS (
                    SELECT 1 FROM "MemberBadges" existing
                    WHERE existing."MemberId"    = member_counts."MemberId"
                      AND existing."SeasonId"    = member_counts."SeasonId"
                      AND existing."BadgeTierId" = bt."Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retroactively awarded badges cannot be safely reversed without knowing
            // which were pre-existing, so Down is intentionally a no-op.
        }
    }
}
