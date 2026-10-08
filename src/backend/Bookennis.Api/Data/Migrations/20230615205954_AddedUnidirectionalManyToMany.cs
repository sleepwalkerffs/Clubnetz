using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedUnidirectionalManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Players", table: "Bookings");

            migrationBuilder.CreateTable(
                name: "BookingEntryPlayer",
                columns: table =>
                    new
                    {
                        BookingEntryId = table.Column<int>(type: "integer", nullable: false),
                        PlayersId = table.Column<int>(type: "integer", nullable: false),
                        PlayerId = table.Column<int>(type: "integer", nullable: false)
                    },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingEntryPlayer", x => new { x.BookingEntryId, x.PlayersId });
                    table.ForeignKey(
                        name: "FK_BookingEntryPlayer_AspNetUsers_PlayersId",
                        column: x => x.PlayersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_BookingEntryPlayer_Bookings_BookingEntryId",
                        column: x => x.BookingEntryId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(name: "IX_BookingEntryPlayer_PlayerId_BookingEntryId", table: "BookingEntryPlayer", columns: new[] { "PlayerId", "BookingEntryId" });

            migrationBuilder.CreateIndex(name: "IX_BookingEntryPlayer_PlayersId", table: "BookingEntryPlayer", column: "PlayersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BookingEntryPlayer");

            migrationBuilder.AddColumn<int[]>(name: "Players", table: "Bookings", type: "integer[]", nullable: false, defaultValue: new int[0]);
        }
    }
}
