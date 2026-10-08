using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndClubUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_AspNetUsers_UserId",
                table: "BookingEntryPlayers");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "BookingEntryPlayers",
                newName: "ClubUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_UserId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_ClubUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_BookingEntryId_UserId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_BookingEntryId_ClubUserId");

            migrationBuilder.CreateSequence<int>(
                name: "ClubUsers_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "ClubUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserRole = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubUsers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubUsers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, null, "User", "User" },
                    { 2, null, "Administrator", "Administrator" }
                });

            migrationBuilder.Sql("""
                                 INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") SELECT "Id", 1 FROM "AspNetUsers";
                                 INSERT INTO "ClubUsers" ("Id", "UserId", "ClubId", "UserRole") SELECT "Id" + 10, "Id", 1, 0 FROM "AspNetUsers";
                                 """);

            migrationBuilder.Sql("""
                                 UPDATE "BookingEntryPlayers" as p  SET "ClubUserId" = cu."Id"
                                 FROM "ClubUsers" cu
                                 WHERE cu."UserId" = "UserId"
                                 """);

            migrationBuilder.CreateIndex(
                name: "IX_ClubUsers_ClubId",
                table: "ClubUsers",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubUsers_UserId",
                table: "ClubUsers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_ClubUserId",
                table: "BookingEntryPlayers",
                column: "ClubUserId",
                principalTable: "ClubUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_ClubUserId",
                table: "BookingEntryPlayers");

            migrationBuilder.DropTable(
                name: "ClubUsers");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropSequence(
                name: "ClubUsers_Id_Sequence");

            migrationBuilder.RenameColumn(
                name: "ClubUserId",
                table: "BookingEntryPlayers",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_ClubUserId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_BookingEntryId_ClubUserId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_BookingEntryId_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_AspNetUsers_UserId",
                table: "BookingEntryPlayers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
