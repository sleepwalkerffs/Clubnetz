using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "ClubAnnouncementAttachments_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubAnnouncements_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "ClubAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CreatedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EmailSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmailAudience = table.Column<int>(type: "integer", nullable: true),
                    EmailRecipientCount = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubAnnouncements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubAnnouncements_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubAnnouncements_Members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClubAnnouncementAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubAnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubAnnouncementAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubAnnouncementAttachments_ClubAnnouncements_ClubAnnouncem~",
                        column: x => x.ClubAnnouncementId,
                        principalTable: "ClubAnnouncements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubAnnouncementAttachmentContents",
                columns: table => new
                {
                    ClubAnnouncementAttachmentId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubAnnouncementAttachmentContents", x => x.ClubAnnouncementAttachmentId);
                    table.ForeignKey(
                        name: "FK_ClubAnnouncementAttachmentContents_ClubAnnouncementAttachme~",
                        column: x => x.ClubAnnouncementAttachmentId,
                        principalTable: "ClubAnnouncementAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubAnnouncementAttachments_ClubAnnouncementId",
                table: "ClubAnnouncementAttachments",
                column: "ClubAnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubAnnouncements_ClubId_IsPinned_PublishedAt",
                table: "ClubAnnouncements",
                columns: new[] { "ClubId", "IsPinned", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubAnnouncements_CreatedByMemberId",
                table: "ClubAnnouncements",
                column: "CreatedByMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubAnnouncementAttachmentContents");

            migrationBuilder.DropTable(
                name: "ClubAnnouncementAttachments");

            migrationBuilder.DropTable(
                name: "ClubAnnouncements");

            migrationBuilder.DropSequence(
                name: "ClubAnnouncementAttachments_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubAnnouncements_Id_Sequence");
        }
    }
}
