using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOneTimeBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "MemberOneTimeBadges_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "OneTimeBadges_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOneTimeBadgeId",
                table: "MemberBadgeSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OneTimeBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneTimeBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OneTimeBadges_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OneTimeBadges_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberOneTimeBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    OneTimeBadgeId = table.Column<int>(type: "integer", nullable: false),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberOneTimeBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberOneTimeBadges_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberOneTimeBadges_OneTimeBadges_OneTimeBadgeId",
                        column: x => x.OneTimeBadgeId,
                        principalTable: "OneTimeBadges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OneTimeBadgeImages",
                columns: table => new
                {
                    OneTimeBadgeId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneTimeBadgeImages", x => x.OneTimeBadgeId);
                    table.ForeignKey(
                        name: "FK_OneTimeBadgeImages_OneTimeBadges_OneTimeBadgeId",
                        column: x => x.OneTimeBadgeId,
                        principalTable: "OneTimeBadges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberBadgeSettings_DisplayOneTimeBadgeId",
                table: "MemberBadgeSettings",
                column: "DisplayOneTimeBadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberOneTimeBadges_OneTimeBadgeId",
                table: "MemberOneTimeBadges",
                column: "OneTimeBadgeId");

            migrationBuilder.CreateIndex(
                name: "UX_MemberOneTimeBadges_MemberId_OneTimeBadgeId",
                table: "MemberOneTimeBadges",
                columns: new[] { "MemberId", "OneTimeBadgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OneTimeBadges_SeasonId",
                table: "OneTimeBadges",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UX_OneTimeBadges_ClubId_SeasonId_Name",
                table: "OneTimeBadges",
                columns: new[] { "ClubId", "SeasonId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberBadgeSettings_MemberOneTimeBadges_DisplayOneTimeBadge~",
                table: "MemberBadgeSettings",
                column: "DisplayOneTimeBadgeId",
                principalTable: "MemberOneTimeBadges",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberBadgeSettings_MemberOneTimeBadges_DisplayOneTimeBadge~",
                table: "MemberBadgeSettings");

            migrationBuilder.DropTable(
                name: "MemberOneTimeBadges");

            migrationBuilder.DropTable(
                name: "OneTimeBadgeImages");

            migrationBuilder.DropTable(
                name: "OneTimeBadges");

            migrationBuilder.DropIndex(
                name: "IX_MemberBadgeSettings_DisplayOneTimeBadgeId",
                table: "MemberBadgeSettings");

            migrationBuilder.DropColumn(
                name: "DisplayOneTimeBadgeId",
                table: "MemberBadgeSettings");

            migrationBuilder.DropSequence(
                name: "MemberOneTimeBadges_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "OneTimeBadges_Id_Sequence");
        }
    }
}
