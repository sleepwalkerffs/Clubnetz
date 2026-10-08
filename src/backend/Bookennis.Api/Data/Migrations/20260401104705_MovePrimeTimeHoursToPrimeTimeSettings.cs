using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MovePrimeTimeHoursToPrimeTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PrimeTimeHours_To",
                table: "Clubs",
                newName: "PrimeTimeSettings_PrimeTimeHours_To");

            migrationBuilder.RenameColumn(
                name: "PrimeTimeHours_From",
                table: "Clubs",
                newName: "PrimeTimeSettings_PrimeTimeHours_From");

            migrationBuilder.RenameIndex(
                name: "IX_Clubs_PrimeTimeHours_To",
                table: "Clubs",
                newName: "IX_Clubs_PrimeTimeSettings_PrimeTimeHours_To");

            migrationBuilder.RenameIndex(
                name: "IX_Clubs_PrimeTimeHours_From",
                table: "Clubs",
                newName: "IX_Clubs_PrimeTimeSettings_PrimeTimeHours_From");

            migrationBuilder.AddColumn<int>(
                name: "PrimeTimeSettings_ChildAgeThreshold",
                table: "Clubs",
                type: "integer",
                nullable: false,
                defaultValue: 18);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimeTimeSettings_ChildAgeThreshold",
                table: "Clubs");

            migrationBuilder.RenameColumn(
                name: "PrimeTimeSettings_PrimeTimeHours_To",
                table: "Clubs",
                newName: "PrimeTimeHours_To");

            migrationBuilder.RenameColumn(
                name: "PrimeTimeSettings_PrimeTimeHours_From",
                table: "Clubs",
                newName: "PrimeTimeHours_From");

            migrationBuilder.RenameIndex(
                name: "IX_Clubs_PrimeTimeSettings_PrimeTimeHours_To",
                table: "Clubs",
                newName: "IX_Clubs_PrimeTimeHours_To");

            migrationBuilder.RenameIndex(
                name: "IX_Clubs_PrimeTimeSettings_PrimeTimeHours_From",
                table: "Clubs",
                newName: "IX_Clubs_PrimeTimeHours_From");
        }
    }
}
