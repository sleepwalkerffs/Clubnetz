using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "MemberSeasons_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "Seasons_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Period_To = table.Column<DateOnly>(type: "date", nullable: false),
                    Period_From = table.Column<DateOnly>(type: "date", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberSeasons_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberSeasons_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberSeasons_SeasonId",
                table: "MemberSeasons",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UX_MemberSeasons_MemberId_SeasonId",
                table: "MemberSeasons",
                columns: new[] { "MemberId", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_ClubId",
                table: "Seasons",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_Period_From",
                table: "Seasons",
                column: "Period_From",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_Period_To",
                table: "Seasons",
                column: "Period_To",
                descending: new bool[0]);

            // Data migration: for each club that has members with IsAllowedToBook=true,
            // create a default season (Apr 1, 2025 - Nov 30, 2025) and link those members.
            migrationBuilder.Sql("""
                -- Insert a default season for every club that has at least one member with IsAllowedToBook = true
                INSERT INTO "Seasons" ("Id", "ClubId", "Period_From", "Period_To", "Metadata_Created")
                SELECT nextval('"Seasons_Id_Sequence"'), c."Id", '2025-04-01', '2025-11-30', NOW()
                FROM "Clubs" c
                WHERE EXISTS (SELECT 1 FROM "Members" m WHERE m."ClubId" = c."Id" AND m."IsAllowedToBook" = true);

                -- Link all members with IsAllowedToBook=true to their club's newly created season
                INSERT INTO "MemberSeasons" ("Id", "MemberId", "SeasonId", "Metadata_Created")
                SELECT nextval('"MemberSeasons_Id_Sequence"'), m."Id", s."Id", NOW()
                FROM "Members" m
                INNER JOIN "Seasons" s ON s."ClubId" = m."ClubId"
                WHERE m."IsAllowedToBook" = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberSeasons");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropSequence(
                name: "MemberSeasons_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "Seasons_Id_Sequence");
        }
    }
}
