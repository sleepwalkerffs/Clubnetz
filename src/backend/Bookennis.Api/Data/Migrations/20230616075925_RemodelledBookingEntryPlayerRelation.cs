using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemodelledBookingEntryPlayerRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_BookingEntryPlayers_AspNetUsers_PlayersId", table: "BookingEntryPlayers");

            migrationBuilder.DropPrimaryKey(name: "PK_BookingEntryPlayers", table: "BookingEntryPlayers");

            migrationBuilder.DropIndex(name: "IX_BookingEntryPlayers_PlayerId_BookingEntryId", table: "BookingEntryPlayers");

            migrationBuilder.DropIndex(name: "IX_BookingEntryPlayers_PlayersId", table: "BookingEntryPlayers");

            migrationBuilder.RenameColumn(name: "PlayerId", table: "BookingEntryPlayers", newName: "UserId");

            migrationBuilder.RenameColumn(name: "PlayersId", table: "BookingEntryPlayers", newName: "Id");

            migrationBuilder.CreateSequence<int>(name: "BookingEntryPlayers_Id_Sequence", startValue: 10L, incrementBy: 10);

            migrationBuilder.AddColumn<DateTime>(name: "Metadata_Created", table: "BookingEntryPlayers", type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<int>(name: "Metadata_CreationUserId", table: "BookingEntryPlayers", type: "integer", nullable: true);

            migrationBuilder.AddColumn<int>(name: "Metadata_ModificationUserId", table: "BookingEntryPlayers", type: "integer", nullable: true);

            migrationBuilder.AddColumn<DateTime>(name: "Metadata_Modified", table: "BookingEntryPlayers", type: "timestamp with time zone", nullable: true);

            migrationBuilder.AddPrimaryKey(name: "PK_BookingEntryPlayers", table: "BookingEntryPlayers", column: "Id");

            migrationBuilder.CreateIndex(name: "IX_BookingEntryPlayers_BookingEntryId_UserId", table: "BookingEntryPlayers", columns: new[] { "BookingEntryId", "UserId" });

            migrationBuilder.CreateIndex(name: "IX_BookingEntryPlayers_UserId", table: "BookingEntryPlayers", column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_AspNetUsers_UserId",
                table: "BookingEntryPlayers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_BookingEntryPlayers_AspNetUsers_UserId", table: "BookingEntryPlayers");

            migrationBuilder.DropPrimaryKey(name: "PK_BookingEntryPlayers", table: "BookingEntryPlayers");

            migrationBuilder.DropIndex(name: "IX_BookingEntryPlayers_BookingEntryId_UserId", table: "BookingEntryPlayers");

            migrationBuilder.DropIndex(name: "IX_BookingEntryPlayers_UserId", table: "BookingEntryPlayers");

            migrationBuilder.DropColumn(name: "Metadata_Created", table: "BookingEntryPlayers");

            migrationBuilder.DropColumn(name: "Metadata_CreationUserId", table: "BookingEntryPlayers");

            migrationBuilder.DropColumn(name: "Metadata_ModificationUserId", table: "BookingEntryPlayers");

            migrationBuilder.DropColumn(name: "Metadata_Modified", table: "BookingEntryPlayers");

            migrationBuilder.DropSequence(name: "BookingEntryPlayers_Id_Sequence");

            migrationBuilder.RenameColumn(name: "UserId", table: "BookingEntryPlayers", newName: "PlayerId");

            migrationBuilder.RenameColumn(name: "Id", table: "BookingEntryPlayers", newName: "PlayersId");

            migrationBuilder.AddPrimaryKey(name: "PK_BookingEntryPlayers", table: "BookingEntryPlayers", columns: new[] { "BookingEntryId", "PlayersId" });

            migrationBuilder.CreateIndex(name: "IX_BookingEntryPlayers_PlayerId_BookingEntryId", table: "BookingEntryPlayers", columns: new[] { "PlayerId", "BookingEntryId" });

            migrationBuilder.CreateIndex(name: "IX_BookingEntryPlayers_PlayersId", table: "BookingEntryPlayers", column: "PlayersId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_AspNetUsers_PlayersId",
                table: "BookingEntryPlayers",
                column: "PlayersId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
