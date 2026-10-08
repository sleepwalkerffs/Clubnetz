using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultileUserRolesForMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        { 
            migrationBuilder.AddColumn<int[]>(
                name: "UserRoles",
                table: "Members",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.Sql(
                """
                Update "Members" SET "UserRoles" = array_append("UserRoles", "UserRole");
                """);

            migrationBuilder.DropColumn(
                name: "UserRole",
                table: "Members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserRoles",
                table: "Members");

            migrationBuilder.AddColumn<int>(
                name: "UserRole",
                table: "Members",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
