using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamingClubUserToMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_ClubUserId",
                table: "BookingEntryPlayers");

            migrationBuilder.RenameColumn(
                name: "ClubUserId",
                table: "FamilyMembers",
                newName: "MemberId");

            migrationBuilder.RenameIndex(
                name: "UX_FamilyMembers_ClubUserId",
                table: "FamilyMembers",
                newName: "UX_FamilyMembers_MemberId");

            migrationBuilder.RenameColumn(
                name: "ClubUserId",
                table: "BookingEntryPlayers",
                newName: "MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_ClubUserId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_BookingEntryId_ClubUserId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_BookingEntryId_MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_MemberId",
                table: "BookingEntryPlayers",
                column: "MemberId",
                principalTable: "ClubUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

                        migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_MemberId",
                table: "BookingEntryPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClubUsers_AspNetUsers_UserId",
                table: "ClubUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClubUsers_Clubs_ClubId",
                table: "ClubUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClubUsers",
                table: "ClubUsers");

            migrationBuilder.DropSequence(
                name: "ClubUsers_Id_Sequence");

            migrationBuilder.RenameTable(
                name: "ClubUsers",
                newName: "Members");

            migrationBuilder.RenameIndex(
                name: "IX_ClubUsers_UserId",
                table: "Members",
                newName: "IX_Members_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ClubUsers_ClubId",
                table: "Members",
                newName: "IX_Members_ClubId");

            migrationBuilder.CreateSequence<int>(
                name: "Members_Id_Sequence",
                startValue: 500L,
                incrementBy: 10);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Members",
                table: "Members",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_Members_MemberId",
                table: "BookingEntryPlayers",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_AspNetUsers_UserId",
                table: "Members",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Clubs_ClubId",
                table: "Members",
                column: "ClubId",
                principalTable: "Clubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_MemberId",
                table: "BookingEntryPlayers");

            migrationBuilder.RenameColumn(
                name: "MemberId",
                table: "FamilyMembers",
                newName: "ClubUserId");

            migrationBuilder.RenameIndex(
                name: "UX_FamilyMembers_MemberId",
                table: "FamilyMembers",
                newName: "UX_FamilyMembers_ClubUserId");

            migrationBuilder.RenameColumn(
                name: "MemberId",
                table: "BookingEntryPlayers",
                newName: "ClubUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_MemberId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_ClubUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingEntryPlayers_BookingEntryId_MemberId",
                table: "BookingEntryPlayers",
                newName: "IX_BookingEntryPlayers_BookingEntryId_ClubUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_ClubUserId",
                table: "BookingEntryPlayers",
                column: "ClubUserId",
                principalTable: "ClubUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

              migrationBuilder.DropForeignKey(
                name: "FK_BookingEntryPlayers_Members_MemberId",
                table: "BookingEntryPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_AspNetUsers_UserId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Clubs_ClubId",
                table: "Members");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Members",
                table: "Members");

            migrationBuilder.DropSequence(
                name: "Members_Id_Sequence");

            migrationBuilder.RenameTable(
                name: "Members",
                newName: "ClubUsers");

            migrationBuilder.RenameIndex(
                name: "IX_Members_UserId",
                table: "ClubUsers",
                newName: "IX_ClubUsers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Members_ClubId",
                table: "ClubUsers",
                newName: "IX_ClubUsers_ClubId");

            migrationBuilder.CreateSequence<int>(
                name: "ClubUsers_Id_Sequence",
                startValue: 500L,
                incrementBy: 10);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClubUsers",
                table: "ClubUsers",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingEntryPlayers_ClubUsers_MemberId",
                table: "BookingEntryPlayers",
                column: "MemberId",
                principalTable: "ClubUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClubUsers_AspNetUsers_UserId",
                table: "ClubUsers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClubUsers_Clubs_ClubId",
                table: "ClubUsers",
                column: "ClubId",
                principalTable: "Clubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
