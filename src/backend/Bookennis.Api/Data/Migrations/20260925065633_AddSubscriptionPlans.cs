using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "SubscriptionPlanAssignments_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "SubscriptionPlanParticipants_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "SubscriptionPlans_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlayersPerWeek = table.Column<int>(type: "integer", nullable: false),
                    ExcludedWeeks = table.Column<List<DateOnly>>(type: "date[]", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlans_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlans_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Percentage = table.Column<int>(type: "integer", nullable: false),
                    ColorIndex = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UnavailableWeeks = table.Column<List<DateOnly>>(type: "date[]", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanParticipants_SubscriptionPlans_Subscription~",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlanParticipantId = table.Column<int>(type: "integer", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanAssignments_SubscriptionPlanParticipants_Su~",
                        column: x => x.SubscriptionPlanParticipantId,
                        principalTable: "SubscriptionPlanParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanAssignments_SubscriptionPlans_SubscriptionP~",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanAssignments_SubscriptionPlanId_WeekStart",
                table: "SubscriptionPlanAssignments",
                columns: new[] { "SubscriptionPlanId", "WeekStart" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanAssignments_SubscriptionPlanParticipantId",
                table: "SubscriptionPlanAssignments",
                column: "SubscriptionPlanParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanParticipants_SubscriptionPlanId",
                table: "SubscriptionPlanParticipants",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_ClubId_OwnerUserId",
                table: "SubscriptionPlans",
                columns: new[] { "ClubId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_OwnerUserId",
                table: "SubscriptionPlans",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPlanAssignments");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanParticipants");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropSequence(
                name: "SubscriptionPlanAssignments_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "SubscriptionPlanParticipants_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "SubscriptionPlans_Id_Sequence");
        }
    }
}
