using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeBadgeTiersSeasonal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: drop the old club-only uniqueness constraint and add SeasonId as nullable,
            // so the data reshuffle below can run before the schema is locked down to its final shape.
            migrationBuilder.DropIndex(
                name: "UX_BadgeTiers_ClubId_Level",
                table: "BadgeTiers");

            migrationBuilder.AddColumn<int>(
                name: "SeasonId",
                table: "BadgeTiers",
                type: "integer",
                nullable: true);

            // Step 2: badge tiers used to be one global set per club, reused for every season.
            // Split each club's current tier set into a per-season copy for every season that
            // club already has, then repoint already-earned MemberBadges at the copy matching
            // the season they were actually earned in, and finally drop the now-superseded
            // global (season-less) tiers. Clubs with zero seasons simply lose their unreferenced
            // global tiers, since there is no season to attach them to.
            migrationBuilder.Sql("""
                INSERT INTO "BadgeTiers" ("Id", "ClubId", "SeasonId", "Level", "Name", "Description", "MatchesRequired", "SortOrder", "Metadata_Created")
                SELECT
                    nextval('"BadgeTiers_Id_Sequence"'),
                    bt."ClubId",
                    s."Id",
                    bt."Level",
                    bt."Name",
                    bt."Description",
                    bt."MatchesRequired",
                    bt."SortOrder",
                    NOW()
                FROM "BadgeTiers" bt
                JOIN "Seasons" s ON s."ClubId" = bt."ClubId"
                WHERE bt."SeasonId" IS NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "BadgeTierImages" ("BadgeTierId", "Data", "ContentType")
                SELECT
                    new_bt."Id",
                    img."Data",
                    img."ContentType"
                FROM "BadgeTiers" new_bt
                JOIN "BadgeTiers" old_bt
                    ON old_bt."ClubId" = new_bt."ClubId"
                    AND old_bt."Level" = new_bt."Level"
                    AND old_bt."SeasonId" IS NULL
                JOIN "BadgeTierImages" img ON img."BadgeTierId" = old_bt."Id"
                WHERE new_bt."SeasonId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "MemberBadges" mb
                SET "BadgeTierId" = new_bt."Id"
                FROM "BadgeTiers" old_bt, "BadgeTiers" new_bt
                WHERE old_bt."Id" = mb."BadgeTierId"
                    AND old_bt."SeasonId" IS NULL
                    AND new_bt."ClubId" = old_bt."ClubId"
                    AND new_bt."Level" = old_bt."Level"
                    AND new_bt."SeasonId" = mb."SeasonId";
                """);

            migrationBuilder.Sql("""
                DELETE FROM "BadgeTierImages" WHERE "BadgeTierId" IN (SELECT "Id" FROM "BadgeTiers" WHERE "SeasonId" IS NULL);
                DELETE FROM "BadgeTiers" WHERE "SeasonId" IS NULL;
                """);

            // Step 3: lock the schema down to its final, fully-seasonal shape.
            migrationBuilder.AlterColumn<int>(
                name: "SeasonId",
                table: "BadgeTiers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BadgeTiers_SeasonId",
                table: "BadgeTiers",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Level",
                table: "BadgeTiers",
                columns: new[] { "ClubId", "SeasonId", "Level" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BadgeTiers_Seasons_SeasonId",
                table: "BadgeTiers",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The data reshuffle in Up() (splitting global tiers into per-season copies and
            // repointing MemberBadges) cannot be safely reversed - which season a tier "belongs
            // to" pre-migration isn't recoverable. Down() only reverts the structural changes.
            migrationBuilder.DropForeignKey(
                name: "FK_BadgeTiers_Seasons_SeasonId",
                table: "BadgeTiers");

            migrationBuilder.DropIndex(
                name: "IX_BadgeTiers_SeasonId",
                table: "BadgeTiers");

            migrationBuilder.DropIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Level",
                table: "BadgeTiers");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "BadgeTiers");

            migrationBuilder.CreateIndex(
                name: "UX_BadgeTiers_ClubId_Level",
                table: "BadgeTiers",
                columns: new[] { "ClubId", "Level" },
                unique: true);
        }
    }
}
