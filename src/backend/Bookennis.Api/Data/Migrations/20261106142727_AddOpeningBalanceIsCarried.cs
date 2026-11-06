using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalanceIsCarried : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCarried",
                table: "FiscalYearOpeningBalances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Until now a year took every balance from the year before once it was closed into it
            migrationBuilder.Sql("""
                UPDATE "FiscalYearOpeningBalances" b SET "IsCarried" = TRUE
                FROM "FiscalYears" y
                WHERE y."Id" = b."FiscalYearId" AND y."OpeningBalancesCarried";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCarried",
                table: "FiscalYearOpeningBalances");
        }
    }
}
