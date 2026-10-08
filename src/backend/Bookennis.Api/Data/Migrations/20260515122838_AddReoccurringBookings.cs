using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReoccurringBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "RecurringBookingSeries_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "RecurringBookingSeriesPlayers_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRecurring",
                table: "PlayModes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsExcludedFromSeries",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecurringBookingSeriesId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringBookingSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CourtId = table.Column<int>(type: "integer", nullable: false),
                    PlayModeId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZoneInfoId = table.Column<string>(type: "text", nullable: false),
                    RecurrenceIntervalWeeks = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBookingSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBookingSeries_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBookingSeries_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBookingSeries_PlayModes_PlayModeId",
                        column: x => x.PlayModeId,
                        principalTable: "PlayModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBookingSeriesPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RecurringBookingSeriesId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBookingSeriesPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBookingSeriesPlayers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBookingSeriesPlayers_RecurringBookingSeries_Recurr~",
                        column: x => x.RecurringBookingSeriesId,
                        principalTable: "RecurringBookingSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RecurringBookingSeriesId",
                table: "Bookings",
                column: "RecurringBookingSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookingSeries_ClubId",
                table: "RecurringBookingSeries",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookingSeries_CourtId",
                table: "RecurringBookingSeries",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookingSeries_PlayModeId",
                table: "RecurringBookingSeries",
                column: "PlayModeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookingSeriesPlayers_MemberId",
                table: "RecurringBookingSeriesPlayers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBookingSeriesPlayers_RecurringBookingSeriesId_Memb~",
                table: "RecurringBookingSeriesPlayers",
                columns: new[] { "RecurringBookingSeriesId", "MemberId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_RecurringBookingSeries_RecurringBookingSeriesId",
                table: "Bookings",
                column: "RecurringBookingSeriesId",
                principalTable: "RecurringBookingSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_RecurringBookingSeries_RecurringBookingSeriesId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "RecurringBookingSeriesPlayers");

            migrationBuilder.DropTable(
                name: "RecurringBookingSeries");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RecurringBookingSeriesId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AllowRecurring",
                table: "PlayModes");

            migrationBuilder.DropColumn(
                name: "IsExcludedFromSeries",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RecurringBookingSeriesId",
                table: "Bookings");

            migrationBuilder.DropSequence(
                name: "RecurringBookingSeries_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "RecurringBookingSeriesPlayers_Id_Sequence");
        }
    }
}
