using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "AccountingCategories_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "AccountingEntries_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "AccountingReceipts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "AccountingSettings_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "CostCenters_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "FiscalYears_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "MoneyAccounts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "AccountingCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TaxSphere = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingCategories_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingSettings_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostCenters_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    NextEntryNumber = table.Column<int>(type: "integer", nullable: false),
                    OpeningBalancesCarried = table.Column<bool>(type: "boolean", nullable: false),
                    AuditStatus = table.Column<int>(type: "integer", nullable: false),
                    AuditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuditedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    AuditComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalYears_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiscalYears_Members_AuditedByMemberId",
                        column: x => x.AuditedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FiscalYears_Members_ClosedByMemberId",
                        column: x => x.ClosedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MoneyAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoneyAccounts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    ToMoneyAccountId = table.Column<int>(type: "integer", nullable: true),
                    TransferAmountCents = table.Column<int>(type: "integer", nullable: true),
                    CounterpartyMemberId = table.Column<int>(type: "integer", nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByMemberId = table.Column<int>(type: "integer", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingEntries_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountingEntries_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingEntries_Members_CancelledByMemberId",
                        column: x => x.CancelledByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountingEntries_Members_CounterpartyMemberId",
                        column: x => x.CounterpartyMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountingEntries_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingEntries_MoneyAccounts_ToMoneyAccountId",
                        column: x => x.ToMoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FiscalYearId = table.Column<int>(type: "integer", nullable: false),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearOpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalYearOpeningBalances_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiscalYearOpeningBalances_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountingEntryChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountingEntryId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ByMemberId = table.Column<int>(type: "integer", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntryChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingEntryChanges_AccountingEntries_AccountingEntryId",
                        column: x => x.AccountingEntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountingEntryChanges_Members_ByMemberId",
                        column: x => x.ByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEntryLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountingEntryId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    AmountCents = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingEntryLines_AccountingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AccountingCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingEntryLines_AccountingEntries_AccountingEntryId",
                        column: x => x.AccountingEntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountingEntryLines_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountingReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    FileName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingReceipts_AccountingEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingReceipts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountingReceipts_Members_UploadedByMemberId",
                        column: x => x.UploadedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccountingReceiptContents",
                columns: table => new
                {
                    AccountingReceiptId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingReceiptContents", x => x.AccountingReceiptId);
                    table.ForeignKey(
                        name: "FK_AccountingReceiptContents_AccountingReceipts_AccountingRece~",
                        column: x => x.AccountingReceiptId,
                        principalTable: "AccountingReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AccountingCategories_ClubId_Kind_Name",
                table: "AccountingCategories",
                columns: new[] { "ClubId", "Kind", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_CancelledByMemberId",
                table: "AccountingEntries",
                column: "CancelledByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_ClubId_Date",
                table: "AccountingEntries",
                columns: new[] { "ClubId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_CounterpartyMemberId",
                table: "AccountingEntries",
                column: "CounterpartyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_MoneyAccountId",
                table: "AccountingEntries",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_ToMoneyAccountId",
                table: "AccountingEntries",
                column: "ToMoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_AccountingEntries_FiscalYearId_Number",
                table: "AccountingEntries",
                columns: new[] { "FiscalYearId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryChanges_AccountingEntryId",
                table: "AccountingEntryChanges",
                column: "AccountingEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryChanges_ByMemberId",
                table: "AccountingEntryChanges",
                column: "ByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryLines_AccountingEntryId",
                table: "AccountingEntryLines",
                column: "AccountingEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryLines_CategoryId",
                table: "AccountingEntryLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryLines_CostCenterId",
                table: "AccountingEntryLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingReceipts_ClubId_EntryId",
                table: "AccountingReceipts",
                columns: new[] { "ClubId", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingReceipts_EntryId",
                table: "AccountingReceipts",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingReceipts_UploadedByMemberId",
                table: "AccountingReceipts",
                column: "UploadedByMemberId");

            migrationBuilder.CreateIndex(
                name: "UX_AccountingSettings_ClubId",
                table: "AccountingSettings",
                column: "ClubId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CostCenters_ClubId_Name",
                table: "CostCenters",
                columns: new[] { "ClubId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearOpeningBalances_MoneyAccountId",
                table: "FiscalYearOpeningBalances",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_FiscalYearOpeningBalances_FiscalYearId_MoneyAccountId",
                table: "FiscalYearOpeningBalances",
                columns: new[] { "FiscalYearId", "MoneyAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_AuditedByMemberId",
                table: "FiscalYears",
                column: "AuditedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_ClosedByMemberId",
                table: "FiscalYears",
                column: "ClosedByMemberId");

            migrationBuilder.CreateIndex(
                name: "UX_FiscalYears_ClubId_StartDate",
                table: "FiscalYears",
                columns: new[] { "ClubId", "StartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MoneyAccounts_ClubId_Name",
                table: "MoneyAccounts",
                columns: new[] { "ClubId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingEntryChanges");

            migrationBuilder.DropTable(
                name: "AccountingEntryLines");

            migrationBuilder.DropTable(
                name: "AccountingReceiptContents");

            migrationBuilder.DropTable(
                name: "AccountingSettings");

            migrationBuilder.DropTable(
                name: "FiscalYearOpeningBalances");

            migrationBuilder.DropTable(
                name: "AccountingCategories");

            migrationBuilder.DropTable(
                name: "CostCenters");

            migrationBuilder.DropTable(
                name: "AccountingReceipts");

            migrationBuilder.DropTable(
                name: "AccountingEntries");

            migrationBuilder.DropTable(
                name: "FiscalYears");

            migrationBuilder.DropTable(
                name: "MoneyAccounts");

            migrationBuilder.DropSequence(
                name: "AccountingCategories_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "AccountingEntries_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "AccountingReceipts_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "AccountingSettings_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "CostCenters_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "FiscalYears_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "MoneyAccounts_Id_Sequence");
        }
    }
}
