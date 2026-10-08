using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DomainClubRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InactiveTo",
                table: "Courts",
                newName: "Inactive_To");

            migrationBuilder.RenameColumn(
                name: "InactiveFrom",
                table: "Courts",
                newName: "Inactive_From");

            migrationBuilder.RenameColumn(
                name: "DefaultBookingsPerWeek",
                table: "Clubs",
                newName: "ConcurrentAllowedBookings");

            migrationBuilder.RenameColumn(
                name: "To",
                table: "Bookings",
                newName: "Interval_To");

            migrationBuilder.RenameColumn(
                name: "From",
                table: "Bookings",
                newName: "Interval_From");

            migrationBuilder.RenameColumn(name: "PrimeTimeHours_To", table: "Clubs", newName: "PrimeTimeHours_To_Old");
            migrationBuilder.RenameColumn(name: "PrimeTimeHours_From", table: "Clubs", newName: "PrimeTimeHours_From_Old");
            migrationBuilder.RenameColumn(name: "OpeningHours_To", table: "Clubs", newName: "OpeningHours_To_Old");
            migrationBuilder.RenameColumn(name: "OpeningHours_From", table: "Clubs", newName: "OpeningHours_From_Old");

            migrationBuilder.AddColumn<TimeOnly>(name: "PrimeTimeHours_To", table: "Clubs", type: "time without time zone", nullable: false, defaultValue: TimeOnly.MinValue);
            migrationBuilder.AddColumn<TimeOnly>(name: "PrimeTimeHours_From", table: "Clubs", type: "time without time zone", nullable: false, defaultValue: TimeOnly.MinValue);
            migrationBuilder.AddColumn<TimeOnly>(name: "OpeningHours_To", table: "Clubs", type: "time without time zone", nullable: false, defaultValue: TimeOnly.MinValue);
            migrationBuilder.AddColumn<TimeOnly>(name: "OpeningHours_From", table: "Clubs", type: "time without time zone", nullable: false, defaultValue: TimeOnly.MinValue);

            migrationBuilder.Sql("""
                                 UPDATE "Clubs" SET
                                    "PrimeTimeHours_To" = (("PrimeTimeHours_To_Old" - 2) || 'hours')::interval::time without time zone,
                                    "PrimeTimeHours_From" = (("PrimeTimeHours_From_Old" - 2) || 'hours')::interval::time without time zone,
                                    "OpeningHours_To" = (("OpeningHours_To_Old" - 2) || 'hours')::interval::time without time zone,
                                    "OpeningHours_From" = (("OpeningHours_From_Old" - 2) || 'hours')::interval::time without time zone
                                 """);

            migrationBuilder.DropColumn(name: "PrimeTimeHours_To_Old", table: "Clubs");
            migrationBuilder.DropColumn(name: "PrimeTimeHours_From_Old", table: "Clubs");
            migrationBuilder.DropColumn(name: "OpeningHours_To_Old", table: "Clubs");
            migrationBuilder.DropColumn(name: "OpeningHours_From_Old", table: "Clubs");

            migrationBuilder.AddColumn<int>(
                name: "ClubId",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Courts_Inactive_From",
                table: "Courts",
                column: "Inactive_From",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Courts_Inactive_To",
                table: "Courts",
                column: "Inactive_To",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_OpeningHours_From",
                table: "Clubs",
                column: "OpeningHours_From");

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_OpeningHours_To",
                table: "Clubs",
                column: "OpeningHours_To");

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_PrimeTimeHours_From",
                table: "Clubs",
                column: "PrimeTimeHours_From",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_PrimeTimeHours_To",
                table: "Clubs",
                column: "PrimeTimeHours_To",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClubId",
                table: "Bookings",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Interval_From",
                table: "Bookings",
                column: "Interval_From");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Interval_To",
                table: "Bookings",
                column: "Interval_To");

            migrationBuilder.Sql("""UPDATE "Bookings" SET "ClubId" = 1;""");

            migrationBuilder.Sql("""
                                 DELETE FROM "Bookings" WHERE "Interval_From" < '2024-04-01';
                                 UPDATE "Bookings" SET "Interval_From" = ("Interval_From"::timestamp || '+02:00')::timestamptz,
                                                       "Interval_To" = ("Interval_To"::timestamp || '+02:00')::timestamptz;
                                 """);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Clubs_ClubId",
                table: "Bookings",
                column: "ClubId",
                principalTable: "Clubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneInfoId",
                table: "Bookings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                                 UPDATE "Bookings" SET "TimeZoneInfoId" = 'W. Europe Standard Time'
                                 """);

            migrationBuilder.RenameTable(name: "BookingEntryPlayers", newName: "BookingPlayers");

            migrationBuilder.RenameSequence(name: "BookingEntryPlayers_Id_Sequence", newName: "BookingPlayers_Id_Sequence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => throw new NotImplementedException();
    }
}
