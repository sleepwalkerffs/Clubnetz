namespace Bookennis.Api.Data.Migrations;

/// <summary>
/// The data part of <see cref="AddCourtSales"/>: the bookings left on the guest cards become booking credits on a normal club membership.
/// Kept in its own class so a test can run it on seeded guest data. Every statement can run twice without changing anything more.
/// </summary>
public static class GuestCardMigrationSql
{
    /// <summary>
    /// For every guest member whose guest cards hold more bookings than the guest played (sum of <c>PurchasedBookings</c> minus the
    /// number of <c>BookingPlayers</c> rows of the guest member):
    /// <list type="number">
    /// <item>a <c>ClubMember</c> for the same user and club is created when there is none (role User, not enrolled; the id comes from the
    /// HiLo sequence: one <c>nextval</c> uses a whole block, so nothing collides with ids EF hands out),</item>
    /// <item>a <c>BookingCreditGrant</c> (<c>Reason = GuestCardMigration</c>) with the remaining bookings is added to it, unless the member
    /// already has one of that reason.</item>
    /// </list>
    /// Then no guest member may book any more, and the stored club email templates of the removed guest card email (type 3) are deleted.
    /// The guest members themselves stay: their bookings are history.
    /// </summary>
    public const string Sql = """
        CREATE TEMP TABLE "GuestCardBalances" ON COMMIT DROP AS
        SELECT g."UserId", g."ClubId", SUM(g."Balance")::integer AS "Balance"
        FROM (
            SELECT guest."UserId",
                   guest."ClubId",
                   (SELECT COALESCE(SUM(card."PurchasedBookings"), 0) FROM "GuestCards" card WHERE card."GuestMemberId" = guest."Id")
                 - (SELECT COUNT(*) FROM "BookingPlayers" player WHERE player."MemberId" = guest."Id") AS "Balance"
            FROM "Members" guest
            WHERE guest."MemberType" = 1
        ) g
        WHERE g."Balance" > 0
        GROUP BY g."UserId", g."ClubId";

        INSERT INTO "Members" ("Id", "UserId", "ClubId", "UserRoles", "MemberType", "IsAllowedToBook", "BookingsPerWeek", "Metadata_Created")
        SELECT nextval('"Members_Id_Sequence"'), b."UserId", b."ClubId", ARRAY[0]::integer[], 0, false, NULL, NOW()
        FROM "GuestCardBalances" b
        WHERE NOT EXISTS (
            SELECT 1 FROM "Members" m WHERE m."UserId" = b."UserId" AND m."ClubId" = b."ClubId" AND m."MemberType" = 0);

        INSERT INTO "BookingCreditGrants" ("Id", "MemberId", "Count", "Reason", "Note", "GrantedByMemberId", "ClubId", "Metadata_Created")
        SELECT nextval('"BookingCreditGrants_Id_Sequence"'), member."Id", b."Balance", 0, NULL, NULL, b."ClubId", NOW()
        FROM "GuestCardBalances" b
        JOIN LATERAL (
            SELECT m."Id" FROM "Members" m
            WHERE m."UserId" = b."UserId" AND m."ClubId" = b."ClubId" AND m."MemberType" = 0
            ORDER BY m."Id"
            LIMIT 1
        ) member ON TRUE
        WHERE NOT EXISTS (
            SELECT 1 FROM "BookingCreditGrants" grant_row WHERE grant_row."MemberId" = member."Id" AND grant_row."Reason" = 0);

        UPDATE "Members" SET "IsAllowedToBook" = FALSE WHERE "MemberType" = 1 AND "IsAllowedToBook";

        DELETE FROM "ClubEmailTemplates" WHERE "Type" = 3;
        """;
}
