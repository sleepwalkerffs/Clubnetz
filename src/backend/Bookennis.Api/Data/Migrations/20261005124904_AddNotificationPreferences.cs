using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "NotificationPreferences_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadlineReminderSentAt",
                table: "ClubEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyMembers",
                table: "ClubEvents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                table: "ClubEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Push = table.Column<bool>(type: "boolean", nullable: false),
                    Email = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_NotificationPreferences_UserId_Type",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "DeadlineReminderSentAt",
                table: "ClubEvents");

            migrationBuilder.DropColumn(
                name: "NotifyMembers",
                table: "ClubEvents");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "ClubEvents");

            migrationBuilder.DropSequence(
                name: "NotificationPreferences_Id_Sequence");
        }
    }
}
