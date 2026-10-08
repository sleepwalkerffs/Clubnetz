using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "ClubEmailTemplates_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToEmail",
                table: "Clubs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "Clubs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubEmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubEmailTemplates_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ClubEmailTemplates_ClubId_Type_Language",
                table: "ClubEmailTemplates",
                columns: new[] { "ClubId", "Type", "Language" },
                unique: true);

            // TC Vorderland: keep the link to the new members area that used to be hardcoded in the welcome email.
            migrationBuilder.Sql("""
                UPDATE "Clubs"
                SET "WebsiteUrl" = 'https://www.tcvorderland.com/neumitgliederbereich'
                WHERE "Id" = 1 AND "WebsiteUrl" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubEmailTemplates");

            migrationBuilder.DropColumn(
                name: "ReplyToEmail",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "Clubs");

            migrationBuilder.DropSequence(
                name: "ClubEmailTemplates_Id_Sequence");
        }
    }
}
