using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanupStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 UPDATE "AspNetUsers" SET "FirstName" = TRIM("FirstName"),
                                                          "LastName" = TRIM("LastName"),
                                                          "Email" = TRIM("Email"),
                                                          "UserName" = TRIM("UserName")
                                 """);

            migrationBuilder.Sql("""
                                 UPDATE "Clubs" SET "Name" = TRIM("Name")
                                 """);

            migrationBuilder.Sql("""
                                 UPDATE "Courts" SET "Name" = TRIM("Name")
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
