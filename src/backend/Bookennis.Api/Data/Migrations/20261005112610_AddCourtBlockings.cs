using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtBlockings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "CourtBlockingCourts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "CourtBlockingOccurrences_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "CourtBlockings_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "CourtBlockings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TimeZoneInfoId = table.Column<string>(type: "text", nullable: false),
                    RecurrenceIntervalWeeks = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtBlockings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtBlockings_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourtBlockingCourts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CourtBlockingId = table.Column<int>(type: "integer", nullable: false),
                    CourtId = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtBlockingCourts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtBlockingCourts_CourtBlockings_CourtBlockingId",
                        column: x => x.CourtBlockingId,
                        principalTable: "CourtBlockings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourtBlockingCourts_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourtBlockingOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CourtBlockingId = table.Column<int>(type: "integer", nullable: false),
                    Interval_To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Interval_From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtBlockingOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtBlockingOccurrences_CourtBlockings_CourtBlockingId",
                        column: x => x.CourtBlockingId,
                        principalTable: "CourtBlockings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourtBlockingCourts_CourtId",
                table: "CourtBlockingCourts",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "UX_CourtBlockingCourts_CourtBlockingId_CourtId",
                table: "CourtBlockingCourts",
                columns: new[] { "CourtBlockingId", "CourtId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourtBlockingOccurrences_CourtBlockingId",
                table: "CourtBlockingOccurrences",
                column: "CourtBlockingId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtBlockingOccurrences_Interval_From",
                table: "CourtBlockingOccurrences",
                column: "Interval_From",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CourtBlockingOccurrences_Interval_To",
                table: "CourtBlockingOccurrences",
                column: "Interval_To",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CourtBlockings_ClubId",
                table: "CourtBlockings",
                column: "ClubId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourtBlockingCourts");

            migrationBuilder.DropTable(
                name: "CourtBlockingOccurrences");

            migrationBuilder.DropTable(
                name: "CourtBlockings");

            migrationBuilder.DropSequence(
                name: "CourtBlockingCourts_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "CourtBlockingOccurrences_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "CourtBlockings_Id_Sequence");
        }
    }
}
