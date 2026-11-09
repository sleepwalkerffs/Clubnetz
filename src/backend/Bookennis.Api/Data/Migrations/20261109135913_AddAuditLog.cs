using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bookennis.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClubId = table.Column<int>(type: "integer", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubjectUserId = table.Column<int>(type: "integer", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ImpersonatorUserId = table.Column<int>(type: "integer", nullable: true),
                    ImpersonatorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ApiKeyId = table.Column<int>(type: "integer", nullable: true),
                    System = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Request = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ClubId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "ClubId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt_Id",
                table: "AuditEvents",
                columns: new[] { "OccurredAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_SubjectUserId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "SubjectUserId", "OccurredAt" });

            // Append-only: UPDATE and DELETE only pass with the maintenance flag of AuditWriter (retention, pseudonymization of deleted users).
            // A safety net against code that changes the log by mistake, not against somebody with access to the database.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_events_append_only() RETURNS trigger AS $$
                BEGIN
                    IF current_setting('clubnetz.audit_maintenance', true) = 'on' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'AuditEvents are append-only (%)', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_AuditEvents_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "AuditEvents"
                    FOR EACH ROW EXECUTE FUNCTION audit_events_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_AuditEvents_AppendOnly" ON "AuditEvents";
                DROP FUNCTION IF EXISTS audit_events_append_only();
                """);

            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
