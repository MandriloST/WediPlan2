using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wediplan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AuditIModeracija : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "consent_at",
                table: "vendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consent_channel",
                table: "vendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consent_note",
                table: "vendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "consent_requested_at",
                table: "vendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "consent_scope",
                table: "vendors",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "consent_status",
                table: "vendors",
                type: "text",
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<DateTime>(
                name: "data_collected_at",
                table: "vendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "data_source",
                table: "vendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_place_id",
                table: "vendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "vendor_photos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderation_note",
                table: "vendor_photos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "vendor_photos",
                type: "text",
                nullable: false,
                defaultValue: "unreviewed");

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "vendor_photos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_user_id",
                table: "vendor_photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rights_confirmed_at",
                table: "vendor_photos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "vendor_photos",
                type: "text",
                nullable: false,
                defaultValue: "partner");

            migrationBuilder.AddColumn<Guid>(
                name: "uploaded_by_user_id",
                table: "vendor_photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                table: "user_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                table: "user_reviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "imported_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_note",
                table: "imported_reviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_key",
                table: "imported_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "imported_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_status",
                table: "imported_reviews",
                type: "text",
                nullable: false,
                defaultValue: "unverified");

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                table: "imported_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by_user_id",
                table: "imported_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vendors_google_place_id",
                table: "vendors",
                column: "google_place_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_photos_moderation_status",
                table: "vendor_photos",
                column: "moderation_status");

            migrationBuilder.CreateIndex(
                name: "IX_imported_reviews_vendor_id_external_key",
                table: "imported_reviews",
                columns: new[] { "vendor_id", "external_key" },
                unique: true,
                filter: "external_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_actor_user_id",
                table: "audit_log",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id_occurred_at",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_occurred_at",
                table: "audit_log",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_vendors_google_place_id",
                table: "vendors");

            migrationBuilder.DropIndex(
                name: "IX_vendor_photos_moderation_status",
                table: "vendor_photos");

            migrationBuilder.DropIndex(
                name: "IX_imported_reviews_vendor_id_external_key",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "consent_at",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "consent_channel",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "consent_note",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "consent_requested_at",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "consent_scope",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "consent_status",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "data_collected_at",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "data_source",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "google_place_id",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "moderation_note",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "rights_confirmed_at",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "source",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "uploaded_by_user_id",
                table: "vendor_photos");

            migrationBuilder.DropColumn(
                name: "decided_by",
                table: "user_reviews");

            migrationBuilder.DropColumn(
                name: "reject_reason",
                table: "user_reviews");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "evidence_note",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "external_key",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "verification_status",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "verified_at",
                table: "imported_reviews");

            migrationBuilder.DropColumn(
                name: "verified_by_user_id",
                table: "imported_reviews");
        }
    }
}
