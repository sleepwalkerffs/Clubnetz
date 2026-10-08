using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyPolicyAcceptedAtAndLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PrivacyPolicyAcceptedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            // Failed logins now lock the account. Users that were not created through the UserManager (seed data, migrations) may have
            // lockout disabled, so enable it for everyone who can log in. Children without an own login have no email.
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "LockoutEnabled" = TRUE WHERE "Email" IS NOT NULL AND "LockoutEnabled" = FALSE;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivacyPolicyAcceptedAt",
                table: "AspNetUsers");
        }
    }
}
