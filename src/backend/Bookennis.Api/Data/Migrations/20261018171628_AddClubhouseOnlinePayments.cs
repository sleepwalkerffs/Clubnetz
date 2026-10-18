using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubhouseOnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ClubhouseTabs_ClubId_MemberId",
                table: "ClubhouseTabs");

            migrationBuilder.CreateSequence<int>(
                name: "ClubhouseSumUpAccounts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "ClubhouseTabPayments_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "ClubhouseSumUpAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MerchantCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConnectedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    NeedsReconnect = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubhouseSumUpAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubhouseSumUpAccounts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubhouseSumUpAccounts_Members_ConnectedByMemberId",
                        column: x => x.ConnectedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClubhouseTabPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    TabId = table.Column<int>(type: "integer", nullable: false),
                    PayerMemberId = table.Column<int>(type: "integer", nullable: true),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MerchantCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TransactionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubhouseTabPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabPayments_ClubhouseTabs_TabId",
                        column: x => x.TabId,
                        principalTable: "ClubhouseTabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabPayments_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubhouseTabPayments_Members_PayerMemberId",
                        column: x => x.PayerMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseTabs_ClubId_MemberId",
                table: "ClubhouseTabs",
                columns: new[] { "ClubId", "MemberId" },
                unique: true,
                filter: "\"Status\" IN (0, 2) AND \"MemberId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseSumUpAccounts_ConnectedByMemberId",
                table: "ClubhouseSumUpAccounts",
                column: "ConnectedByMemberId");

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseSumUpAccounts_ClubId",
                table: "ClubhouseSumUpAccounts",
                column: "ClubId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabPayments_ClubId",
                table: "ClubhouseTabPayments",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabPayments_PayerMemberId",
                table: "ClubhouseTabPayments",
                column: "PayerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubhouseTabPayments_Status_ExpiresAt",
                table: "ClubhouseTabPayments",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseTabPayments_CheckoutId",
                table: "ClubhouseTabPayments",
                column: "CheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseTabPayments_Reference",
                table: "ClubhouseTabPayments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseTabPayments_TabId",
                table: "ClubhouseTabPayments",
                column: "TabId",
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubhouseSumUpAccounts");

            migrationBuilder.DropTable(
                name: "ClubhouseTabPayments");

            migrationBuilder.DropIndex(
                name: "UX_ClubhouseTabs_ClubId_MemberId",
                table: "ClubhouseTabs");

            migrationBuilder.DropSequence(
                name: "ClubhouseSumUpAccounts_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "ClubhouseTabPayments_Id_Sequence");

            migrationBuilder.CreateIndex(
                name: "UX_ClubhouseTabs_ClubId_MemberId",
                table: "ClubhouseTabs",
                columns: new[] { "ClubId", "MemberId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"MemberId\" IS NOT NULL");
        }
    }
}
