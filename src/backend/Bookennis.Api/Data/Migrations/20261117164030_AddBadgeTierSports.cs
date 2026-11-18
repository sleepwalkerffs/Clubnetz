using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeTierSports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Level",
                table: "BadgeTiers");

            migrationBuilder.AddColumn<int>(
                name: "Sport",
                table: "BadgeTiers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Level",
                table: "BadgeTiers",
                columns: new[] { "ClubId", "SeasonId", "Level" },
                unique: true,
                filter: "\"Sport\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Sport_Level",
                table: "BadgeTiers",
                columns: new[] { "ClubId", "SeasonId", "Sport", "Level" },
                unique: true,
                filter: "\"Sport\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Level",
                table: "BadgeTiers");

            migrationBuilder.DropIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Sport_Level",
                table: "BadgeTiers");

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "BadgeTiers");

            migrationBuilder.CreateIndex(
                name: "UX_BadgeTiers_ClubId_SeasonId_Level",
                table: "BadgeTiers",
                columns: new[] { "ClubId", "SeasonId", "Level" },
                unique: true);
        }
    }
}
