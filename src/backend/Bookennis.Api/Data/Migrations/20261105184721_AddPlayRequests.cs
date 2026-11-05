using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "PlayerGroupMembers_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "PlayerGroups_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "PlayRequestInvitees_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "PlayRequests_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "PlayerGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OwnerMemberId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGroups_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerGroups_Members_OwnerMemberId",
                        column: x => x.OwnerMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RequesterMemberId = table.Column<int>(type: "integer", nullable: false),
                    PlayModeId = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeZoneInfoId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CourtIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    PlayersNeeded = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayRequests_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlayRequests_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayRequests_Members_RequesterMemberId",
                        column: x => x.RequesterMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PlayerGroupId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGroupMembers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerGroupMembers_PlayerGroups_PlayerGroupId",
                        column: x => x.PlayerGroupId,
                        principalTable: "PlayerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayRequestInvitees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PlayRequestId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayRequestInvitees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayRequestInvitees_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayRequestInvitees_PlayRequests_PlayRequestId",
                        column: x => x.PlayRequestId,
                        principalTable: "PlayRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGroupMembers_MemberId",
                table: "PlayerGroupMembers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerGroupMembers_PlayerGroupId_MemberId",
                table: "PlayerGroupMembers",
                columns: new[] { "PlayerGroupId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGroups_ClubId",
                table: "PlayerGroups",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGroups_OwnerMemberId",
                table: "PlayerGroups",
                column: "OwnerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayRequestInvitees_MemberId",
                table: "PlayRequestInvitees",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayRequestInvitees_PlayRequestId_MemberId",
                table: "PlayRequestInvitees",
                columns: new[] { "PlayRequestId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayRequests_BookingId",
                table: "PlayRequests",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayRequests_ClubId_Status_StartsAt",
                table: "PlayRequests",
                columns: new[] { "ClubId", "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayRequests_RequesterMemberId",
                table: "PlayRequests",
                column: "RequesterMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerGroupMembers");

            migrationBuilder.DropTable(
                name: "PlayRequestInvitees");

            migrationBuilder.DropTable(
                name: "PlayerGroups");

            migrationBuilder.DropTable(
                name: "PlayRequests");

            migrationBuilder.DropSequence(
                name: "PlayerGroupMembers_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "PlayerGroups_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "PlayRequestInvitees_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "PlayRequests_Id_Sequence");
        }
    }
}
