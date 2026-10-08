using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IsAtpAndClubPrimeTimeHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAtpPlayer",
                table: "Members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PrimeTimeHours_From",
                table: "Clubs",
                type: "integer",
                nullable: false,
                defaultValue: 17);

            migrationBuilder.AddColumn<int>(
                name: "PrimeTimeHours_To",
                table: "Clubs",
                type: "integer",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAtpPlayer",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PrimeTimeHours_From",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "PrimeTimeHours_To",
                table: "Clubs");
        }
    }
}
