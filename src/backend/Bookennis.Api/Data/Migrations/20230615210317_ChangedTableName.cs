using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangedTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayer_AspNetUsers_PlayersId",
                table: "BookingEntryPlayer");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayer_Bookings_BookingEntryId",
                table: "BookingEntryPlayer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookingEntryPlayer",
                table: "BookingEntryPlayer");

            migrationBuilder.RenameTable(
                name: "BookingEntryPlayer",
                newName: "BookingEntryPlayers");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayer_PlayersId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_PlayersId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayer_PlayerId_BookingEntryId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_PlayerId_BookingEntryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookingEntryPlayers",
                table: "BookingEntryPlayers",
                columns: new[] { "BookingEntryId", "PlayersId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_AspNetUsers_PlayersId",
                table: "BookingEntryPlayers",
                column: "PlayersId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_Bookings_BookingEntryId",
                table: "BookingEntryPlayers",
                column: "BookingEntryId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_AspNetUsers_PlayersId",
                table: "BookingEntryPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_Bookings_BookingEntryId",
                table: "BookingEntryPlayers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookingEntryPlayers",
                table: "BookingEntryPlayers");

            migrationBuilder.RenameTable(
                name: "BookingEntryPlayers",
                newName: "BookingEntryPlayer");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_PlayersId",
                table: "BookingEntryPlayer",
                newName: "IX_BookingEntryPlayer_PlayersId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_PlayerId_BookingEntryId",
                table: "BookingEntryPlayer",
                newName: "IX_BookingEntryPlayer_PlayerId_BookingEntryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookingEntryPlayer",
                table: "BookingEntryPlayer",
                columns: new[] { "BookingEntryId", "PlayersId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayer_AspNetUsers_PlayersId",
                table: "BookingEntryPlayer",
                column: "PlayersId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayer_Bookings_BookingEntryId",
                table: "BookingEntryPlayer",
                column: "BookingEntryId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
