using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipOrderSports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "Sports",
                table: "MembershipOrders",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            // Orders keep the sports of their plan from now on
            migrationBuilder.Sql("""
                UPDATE "MembershipOrders" o
                SET "Sports" = p."Sports"
                FROM "MembershipPlans" p
                WHERE p."Id" = o."MembershipPlanId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sports",
                table: "MembershipOrders");
        }
    }
}
