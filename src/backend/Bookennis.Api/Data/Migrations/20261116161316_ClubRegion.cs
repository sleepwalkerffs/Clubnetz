using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClubRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Country",
                table: "Clubs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Clubs",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "eur");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Clubs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Europe/Vienna");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Clubs");
        }
    }
}
