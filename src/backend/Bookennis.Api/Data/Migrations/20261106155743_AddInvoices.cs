using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "Invoices_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "InvoiceSettings_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    German = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NumberYear = table.Column<int>(type: "integer", nullable: true),
                    NumberSequence = table.Column<int>(type: "integer", nullable: true),
                    RecipientMemberId = table.Column<int>(type: "integer", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ServicePeriod = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalCents = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentTo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ReminderCount = table.Column<int>(type: "integer", nullable: false),
                    LastReminderAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_AccountingEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_Members_RecipientMemberId",
                        column: x => x.RecipientMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ZvrNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    Bic = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    NumberPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PaymentTermDays = table.Column<int>(type: "integer", nullable: false),
                    Footer = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSettings_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceSettings_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    UnitPriceCents = table.Column<int>(type: "integer", nullable: false),
                    TotalCents = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_AccountingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AccountingCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceLines_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CategoryId",
                table: "InvoiceLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CostCenterId",
                table: "InvoiceLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClubId_Status",
                table: "Invoices",
                columns: new[] { "ClubId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_RecipientMemberId",
                table: "Invoices",
                column: "RecipientMemberId");

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_ClubId_NumberYear_NumberSequence",
                table: "Invoices",
                columns: new[] { "ClubId", "NumberYear", "NumberSequence" },
                unique: true,
                filter: "\"NumberSequence\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_EntryId",
                table: "Invoices",
                column: "EntryId",
                unique: true,
                filter: "\"EntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSettings_MoneyAccountId",
                table: "InvoiceSettings",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_InvoiceSettings_ClubId",
                table: "InvoiceSettings",
                column: "ClubId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "InvoiceSettings");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropSequence(
                name: "Invoices_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "InvoiceSettings_Id_Sequence");
        }
    }
}
