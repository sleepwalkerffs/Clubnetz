using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubhouseTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "ClubhouseItems_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubhouseTabLines_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubhouseTabs_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "ClubhouseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubhouseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubhouseItems_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubhouseTabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: true),
                    GuestName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpenedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    PaidToMemberId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubhouseTabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabs_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabs_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabs_Members_OpenedByMemberId",
                        column: x => x.OpenedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabs_Members_PaidToMemberId",
                        column: x => x.PaidToMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClubhouseTabLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubhouseTabId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceCents = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    AddedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubhouseTabLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabLines_ClubhouseItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ClubhouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabLines_ClubhouseTabs_ClubhouseTabId",
                        column: x => x.ClubhouseTabId,
                        principalTable: "ClubhouseTabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabLines_Members_AddedByMemberId",
                        column: x => x.AddedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseItems_ClubId",
                table: "ClubhouseItems",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabLines_AddedByMemberId",
                table: "ClubhouseTabLines",
                column: "AddedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabLines_ClubhouseTabId",
                table: "ClubhouseTabLines",
                column: "ClubhouseTabId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabLines_ItemId",
                table: "ClubhouseTabLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabs_ClubId_Status_PaidAt",
                table: "ClubhouseTabs",
                columns: new[] { "ClubId", "Status", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabs_MemberId",
                table: "ClubhouseTabs",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabs_OpenedByMemberId",
                table: "ClubhouseTabs",
                column: "OpenedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabs_PaidToMemberId",
                table: "ClubhouseTabs",
                column: "PaidToMemberId");

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseTabs_ClubId_MemberId",
                table: "ClubhouseTabs",
                columns: new[] { "ClubId", "MemberId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"MemberId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubhouseTabLines");

            migrationBuilder.DropTable(
                name: "ClubhouseItems");

            migrationBuilder.DropTable(
                name: "ClubhouseTabs");

            migrationBuilder.DropSequence(
                name: "ClubhouseItems_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubhouseTabLines_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubhouseTabs_Id_Sequence");
        }
    }
}
