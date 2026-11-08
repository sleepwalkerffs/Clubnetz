using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "WeatherForecasts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndoor",
                table: "Courts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Clubs",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Clubs",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeatherForecasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModified = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Periods = table.Column<string>(type: "jsonb", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherForecasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherForecasts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_WeatherForecasts_ClubId",
                table: "WeatherForecasts",
                column: "ClubId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherForecasts");

            migrationBuilder.DropColumn(
                name: "IsIndoor",
                table: "Courts");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Clubs");

            migrationBuilder.DropSequence(
                name: "WeatherForecasts_Id_Sequence");
        }
    }
}
