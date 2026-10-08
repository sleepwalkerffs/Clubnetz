using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedConfigurablePlayModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlayMode",
                table: "Bookings",
                newName: "PlayModeId");

            migrationBuilder.CreateSequence<int>(
                name: "PlayModes_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayModes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AllowedRoles = table.Column<int[]>(type: "integer[]", nullable: false),
                    CanOverbook = table.Column<bool>(type: "boolean", nullable: false),
                    Color = table.Column<int>(type: "integer", nullable: false),
                    FixedPlayerCount = table.Column<int>(type: "integer", nullable: true),
                    IsChargingBookingSubscription = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FixedDuration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CommentAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayModes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayModes_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
               table: "PlayModes",
               columns: new[] { "Id", "AllowedRoles", "ClubId", "Color", "FixedDuration", "FixedPlayerCount", "IsChargingBookingSubscription", "Name", "CanOverbook", "CommentAllowed" },
               values: new object[,]
               {
                    { 1, new[] { 0, 2 }, 1, -6370972, new TimeSpan(0, 1, 0, 0, 0), 2, true, "Einzel", false, false },
                    { 2, new[] { 0, 2 }, 1, -6370972, new TimeSpan(0, 1, 30, 0, 0), 4, true, "Doppel", false, false }
               });

            migrationBuilder.Sql("""
                UPDATE "Bookings" SET "PlayModeId" = "PlayModeId" + 1
                """);


            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PlayModeId",
                table: "Bookings",
                column: "PlayModeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayModes_ClubId",
                table: "PlayModes",
                column: "ClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_PlayModes_PlayModeId",
                table: "Bookings",
                column: "PlayModeId",
                principalTable: "PlayModes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_PlayModes_PlayModeId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "PlayModes");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PlayModeId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Bookings");

            migrationBuilder.DropSequence(
                name: "PlayModes_Id_Sequence");

            migrationBuilder.RenameColumn(
                name: "PlayModeId",
                table: "Bookings",
                newName: "PlayMode");
        }
    }
}
