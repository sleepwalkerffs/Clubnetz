using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "AuditEvents",
                type: "text",
                nullable: false,
                computedColumnSql: "coalesce(\"Action\", '') || ' ' || coalesce(\"Summary\", '') || ' ' || coalesce(\"ActorName\", '') || ' ' || coalesce(\"ActorEmail\", '') || ' ' || coalesce(\"SubjectName\", '') || ' ' || coalesce(\"ImpersonatorName\", '') || ' ' || coalesce(\"EntityType\", '') || ' ' || coalesce(\"EntityId\", '') || ' ' || coalesce(\"IpAddress\", '') || ' ' || coalesce(\"ErrorCode\", '') || ' ' || coalesce(\"Request\", '') || ' ' || coalesce(\"System\", '')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_SearchText",
                table: "AuditEvents",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_SearchText",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "AuditEvents");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
