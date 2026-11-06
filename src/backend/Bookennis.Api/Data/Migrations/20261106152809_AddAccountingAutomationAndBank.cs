using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingAutomationAndBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "AccountingProposals_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "AccountingSourceSettings_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "BankCsvLayouts_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "BankRules_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "BankStatementImports_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.CreateSequence<int>(
                name: "BankTransactions_Id_Sequence",
                startValue: 10L,
                incrementBy: 10);

            migrationBuilder.AddColumn<int>(
                name: "BankMoneyAccountId",
                table: "AccountingSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardMoneyAccountId",
                table: "AccountingSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CashMoneyAccountId",
                table: "AccountingSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GermanTexts",
                table: "AccountingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "OnlineMoneyAccountId",
                table: "AccountingSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountingProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    ToMoneyAccountId = table.Column<int>(type: "integer", nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    CounterpartyMemberId = table.Column<int>(type: "integer", nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    Problem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingProposals_AccountingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AccountingCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingProposals_AccountingEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingProposals_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountingProposals_Members_CounterpartyMemberId",
                        column: x => x.CounterpartyMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountingProposals_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingProposals_MoneyAccounts_ToMoneyAccountId",
                        column: x => x.ToMoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountingSourceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Since = table.Column<DateOnly>(type: "date", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingSourceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingSourceSettings_AccountingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AccountingCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountingSourceSettings_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankCsvLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    Columns = table.Column<string>(type: "jsonb", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankCsvLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankCsvLayouts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankCsvLayouts_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BankRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MatchText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MatchIban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankRules_AccountingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AccountingCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankRules_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankRules_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankRules_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BankStatementImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OpeningBalanceCents = table.Column<long>(type: "bigint", nullable: true),
                    ClosingBalanceCents = table.Column<long>(type: "bigint", nullable: true),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ImportedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    ImportedByMemberId = table.Column<int>(type: "integer", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_Members_ImportedByMemberId",
                        column: x => x.ImportedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BankStatementImports_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MoneyAccountId = table.Column<int>(type: "integer", nullable: false),
                    ImportId = table.Column<int>(type: "integer", nullable: false),
                    BookingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    CounterpartyName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    CounterpartyIban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Metadata_CreationUserId = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata_ModificationUserId = table.Column<int>(type: "integer", nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransactions_AccountingEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "AccountingEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankStatementImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "BankStatementImports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankTransactions_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactions_MoneyAccounts_MoneyAccountId",
                        column: x => x.MoneyAccountId,
                        principalTable: "MoneyAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSettings_BankMoneyAccountId",
                table: "AccountingSettings",
                column: "BankMoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSettings_CardMoneyAccountId",
                table: "AccountingSettings",
                column: "CardMoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSettings_CashMoneyAccountId",
                table: "AccountingSettings",
                column: "CashMoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSettings_OnlineMoneyAccountId",
                table: "AccountingSettings",
                column: "OnlineMoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingProposals_CategoryId",
                table: "AccountingProposals",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingProposals_ClubId_Status",
                table: "AccountingProposals",
                columns: new[] { "ClubId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingProposals_CounterpartyMemberId",
                table: "AccountingProposals",
                column: "CounterpartyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingProposals_EntryId",
                table: "AccountingProposals",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingProposals_MoneyAccountId",
                table: "AccountingProposals",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingProposals_ToMoneyAccountId",
                table: "AccountingProposals",
                column: "ToMoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_AccountingProposals_ClubId_SourceKey",
                table: "AccountingProposals",
                columns: new[] { "ClubId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSourceSettings_CategoryId",
                table: "AccountingSourceSettings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "UX_AccountingSourceSettings_ClubId_Source",
                table: "AccountingSourceSettings",
                columns: new[] { "ClubId", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankCsvLayouts_ClubId",
                table: "BankCsvLayouts",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "UX_BankCsvLayouts_MoneyAccountId",
                table: "BankCsvLayouts",
                column: "MoneyAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankRules_CategoryId",
                table: "BankRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BankRules_ClubId",
                table: "BankRules",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_BankRules_CostCenterId",
                table: "BankRules",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_BankRules_MoneyAccountId",
                table: "BankRules",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_ClubId_MoneyAccountId_ImportedAt",
                table: "BankStatementImports",
                columns: new[] { "ClubId", "MoneyAccountId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_ImportedByMemberId",
                table: "BankStatementImports",
                column: "ImportedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImports_MoneyAccountId",
                table: "BankStatementImports",
                column: "MoneyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_ClubId_Status_BookingDate",
                table: "BankTransactions",
                columns: new[] { "ClubId", "Status", "BookingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_ImportId",
                table: "BankTransactions",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "UX_BankTransactions_EntryId_MoneyAccountId",
                table: "BankTransactions",
                columns: new[] { "EntryId", "MoneyAccountId" },
                unique: true,
                filter: "\"EntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BankTransactions_MoneyAccountId_Fingerprint",
                table: "BankTransactions",
                columns: new[] { "MoneyAccountId", "Fingerprint" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_BankMoneyAccountId",
                table: "AccountingSettings",
                column: "BankMoneyAccountId",
                principalTable: "MoneyAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_CardMoneyAccountId",
                table: "AccountingSettings",
                column: "CardMoneyAccountId",
                principalTable: "MoneyAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_CashMoneyAccountId",
                table: "AccountingSettings",
                column: "CashMoneyAccountId",
                principalTable: "MoneyAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_OnlineMoneyAccountId",
                table: "AccountingSettings",
                column: "OnlineMoneyAccountId",
                principalTable: "MoneyAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_BankMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_CardMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_CashMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingSettings_MoneyAccounts_OnlineMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropTable(
                name: "AccountingProposals");

            migrationBuilder.DropTable(
                name: "AccountingSourceSettings");

            migrationBuilder.DropTable(
                name: "BankCsvLayouts");

            migrationBuilder.DropTable(
                name: "BankRules");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropTable(
                name: "BankStatementImports");

            migrationBuilder.DropIndex(
                name: "IX_AccountingSettings_BankMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropIndex(
                name: "IX_AccountingSettings_CardMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropIndex(
                name: "IX_AccountingSettings_CashMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropIndex(
                name: "IX_AccountingSettings_OnlineMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "BankMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "CardMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "CashMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "GermanTexts",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "OnlineMoneyAccountId",
                table: "AccountingSettings");

            migrationBuilder.DropSequence(
                name: "AccountingProposals_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "AccountingSourceSettings_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "BankCsvLayouts_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "BankRules_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "BankStatementImports_Id_Sequence");

            migrationBuilder.DropSequence(
                name: "BankTransactions_Id_Sequence");
        }
    }
}
