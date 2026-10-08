using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGamificationBadgeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "BadgeTiers_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "MemberBadges_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "BadgeTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MatchesRequired = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeTiers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BadgeTierImages",
                columns: table => new
                {
                    BadgeTierId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeTierImages", x => x.BadgeTierId);
                    table.ForeignKey(
                        name: "FK_BadgeTierImages_BadgeTiers_BadgeTierId",
                        column: x => x.BadgeTierId,
                        principalTable: "BadgeTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    BadgeTierId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    EarnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberBadges_BadgeTiers_BadgeTierId",
                        column: x => x.BadgeTierId,
                        principalTable: "BadgeTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberBadges_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberBadges_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberBadgeSettings",
                columns: table => new
                {
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    DisplayBadgeId = table.Column<int>(type: "integer", nullable: true),
                    TrophyCasePublic = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberBadgeSettings", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_MemberBadgeSettings_MemberBadges_DisplayBadgeId",
                        column: x => x.DisplayBadgeId,
                        principalTable: "MemberBadges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MemberBadgeSettings_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_BadgeTiers_ClubId_Level",
                table: "BadgeTiers",
                columns: new[] { "ClubId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_BadgeTierId",
                table: "MemberBadges",
                column: "BadgeTierId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadges_SeasonId",
                table: "MemberBadges",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UX_MemberBadges_MemberId_BadgeTierId_SeasonId",
                table: "MemberBadges",
                columns: new[] { "MemberId", "BadgeTierId", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadgeSettings_DisplayBadgeId",
                table: "MemberBadgeSettings",
                column: "DisplayBadgeId");

            // Seed default 8-tier badge structure for all existing clubs
            migrationBuilder.Sql("""
                INSERT INTO "BadgeTiers" ("Id", "ClubId", "Level", "Name", "Description", "MatchesRequired", "SortOrder", "Metadata_Created")
                SELECT
                    nextval('"BadgeTiers_Id_Sequence"'),
                    c."Id",
                    t."Level",
                    t."Name",
                    t."Description",
                    t."MatchesRequired",
                    t."Level",
                    NOW()
                FROM "Clubs" c
                CROSS JOIN (VALUES
                    (1, 'Sandler', 'Du stehst endlich am Platz, aber eigentlich bist du nur für das Bier danach hier!', 3),
                    (2, 'Grundlinien-Junkie', 'Du hast deinen Rhythmus gefunden. Die Grundlinie ist dein Zuhause.', 10),
                    (3, 'Wadlbeißer', 'Ein zäher Gegner! Du kämpfst um jeden einzelnen Punkt und gibst nie auf.', 20),
                    (4, 'Club-Inventar', 'Die Clubmitglieder zahlen schon Miete an dich, weil du immer da bist.', 35),
                    (5, 'Tiebreak-Titan', 'Ernsthafter Enthusiast. Durchschnittlich solide 2 Matches pro Woche.', 50),
                    (6, 'Ballermann', 'Du spielst schwer, schlägst hart und lebst praktisch auf dem Platz.', 60),
                    (7, 'Grand-Slam-Garant', 'Elite-Stufe. Du klopfst an die Tür des absolut legendären Status.', 70),
                    (8, 'Platzhirsch', 'Der König/die Königin der Anlage. Wenn du den Platz betrittst, verneigt sich das Netz.', 75)
                ) AS t("Level", "Name", "Description", "MatchesRequired");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BadgeTierImages");

            migrationBuilder.DropTable(
                name: "MemberBadgeSettings");

            migrationBuilder.DropTable(
                name: "MemberBadges");

            migrationBuilder.DropTable(
                name: "BadgeTiers");

            migrationBuilder.DropSequence(
                name: "BadgeTiers_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "MemberBadges_Id_Sequence");
        }
    }
}
