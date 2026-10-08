using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeClubOpeningHoursToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
               name: "OpeningHours_From",
               table: "Clubs");

            migrationBuilder.DropColumn(
                name: "OpeningHours_To",
                table: "Clubs");

            migrationBuilder.AddColumn<int>(
                name: "OpeningHours_From",
                table: "Clubs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OpeningHours_To",
                table: "Clubs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningHours_From",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "OpeningHours_To",
                table: "Clubs");
        }
    }
}
