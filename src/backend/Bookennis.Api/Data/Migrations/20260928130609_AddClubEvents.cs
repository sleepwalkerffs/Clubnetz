using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "ClubEventQuestionOptions_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubEventQuestions_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubEventRegistrationAnswers_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubEventRegistrations_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubEvents_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "ClubEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CreatedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    RegistrationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxParticipants = table.Column<int>(type: "integer", nullable: true),
                    RegistrationDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEvents_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubEvents_Members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClubEventQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubEventId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SelectionMode = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AllowQuantities = table.Column<bool>(type: "boolean", nullable: false),
                    LimitQuantityToHeadCount = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventQuestions_ClubEvents_ClubEventId",
                        column: x => x.ClubEventId,
                        principalTable: "ClubEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubEventRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubEventId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    HeadCount = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventRegistrations_ClubEvents_ClubEventId",
                        column: x => x.ClubEventId,
                        principalTable: "ClubEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubEventRegistrations_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubEventQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubEventQuestionId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventQuestionOptions_ClubEventQuestions_ClubEventQuesti~",
                        column: x => x.ClubEventQuestionId,
                        principalTable: "ClubEventQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubEventRegistrationAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubEventRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    ClubEventQuestionOptionId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEventRegistrationAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEventRegistrationAnswers_ClubEventQuestionOptions_ClubE~",
                        column: x => x.ClubEventQuestionOptionId,
                        principalTable: "ClubEventQuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubEventRegistrationAnswers_ClubEventRegistrations_ClubEve~",
                        column: x => x.ClubEventRegistrationId,
                        principalTable: "ClubEventRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventQuestionOptions_ClubEventQuestionId",
                table: "ClubEventQuestionOptions",
                column: "ClubEventQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventQuestions_ClubEventId",
                table: "ClubEventQuestions",
                column: "ClubEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventRegistrationAnswers_ClubEventQuestionOptionId",
                table: "ClubEventRegistrationAnswers",
                column: "ClubEventQuestionOptionId");

            migrationBuilder.CreateIndex(
                name: "UX_ClubEventRegistrationAnswers_ClubEventRegistrationId_ClubEv~",
                table: "ClubEventRegistrationAnswers",
                columns: new[] { "ClubEventRegistrationId", "ClubEventQuestionOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubEventRegistrations_MemberId",
                table: "ClubEventRegistrations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "UX_ClubEventRegistrations_ClubEventId_MemberId",
                table: "ClubEventRegistrations",
                columns: new[] { "ClubEventId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubEvents_ClubId_StartDate",
                table: "ClubEvents",
                columns: new[] { "ClubId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEvents_CreatedByMemberId",
                table: "ClubEvents",
                column: "CreatedByMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubEventRegistrationAnswers");

            migrationBuilder.DropTable(
                name: "ClubEventQuestionOptions");

            migrationBuilder.DropTable(
                name: "ClubEventRegistrations");

            migrationBuilder.DropTable(
                name: "ClubEventQuestions");

            migrationBuilder.DropTable(
                name: "ClubEvents");

            migrationBuilder.DropSequence(
                name: "ClubEventQuestionOptions_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubEventQuestions_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubEventRegistrationAnswers_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubEventRegistrations_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubEvents_Id_Sequence");
        }
    }
}
