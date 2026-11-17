using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountingUsBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "MoneyAccounts",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutingNumber",
                table: "MoneyAccounts",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "MoneyAccounts");

            migrationBuilder.DropColumn(
                name: "RoutingNumber",
                table: "MoneyAccounts");
        }
    }
}
