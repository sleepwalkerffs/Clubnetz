using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrimeTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "PrimeTimeSettings_ApplicableWeekdays",
                table: "Clubs",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<bool>(
                name: "PrimeTimeSettings_IsEnabled",
                table: "Clubs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrimeTimeSettings_RestrictChildren",
                table: "Clubs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrimeTimeSettings_RestrictGuests",
                table: "Clubs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimeTimeSettings_ApplicableWeekdays",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "PrimeTimeSettings_IsEnabled",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "PrimeTimeSettings_RestrictChildren",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "PrimeTimeSettings_RestrictGuests",
                table: "Clubs");
        }
    }
}
