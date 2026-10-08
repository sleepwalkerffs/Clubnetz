using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Clubs",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "TC Vorderland" });

            migrationBuilder.InsertData(
                table: "Courts",
                columns: new[] { "Id", "ClubId", "InactiveFrom", "InactiveTo", "Name" },
                values: new object[,]
                {
                    { 1, 1, null, null, "1" },
                    { 2, 1, null, null, "2" },
                    { 3, 1, null, null, "3" },
                    { 4, 1, null, null, "4" },
                    { 5, 1, null, null, "5" },
                    { 6, 1, null, null, "6" },
                    { 7, 1, null, null, "7" }
                });

            migrationBuilder.Sql(""" SELECT setval('"BookingEntryPlayers_Id_Sequence"', 100); """);
            migrationBuilder.Sql(""" SELECT setval('"Bookings_Id_Sequence"', 100); """);
            migrationBuilder.Sql(""" SELECT setval('"Clubs_Id_Sequence"', 100); """);
            migrationBuilder.Sql(""" SELECT setval('"Courts_Id_Sequence"', 100); """);
            migrationBuilder.Sql(""" SELECT setval('"Users_Id_Sequence"', 100); """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Courts",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Clubs",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
