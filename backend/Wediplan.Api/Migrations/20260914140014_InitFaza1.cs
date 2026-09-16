using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Wediplan.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitFaza1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "daily_stats",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    event_name = table.Column<string>(type: "text", nullable: false),
                    category_slug = table.Column<string>(type: "text", nullable: false),
                    region_slug = table.Column<string>(type: "text", nullable: false),
                    vendor_slug = table.Column<string>(type: "text", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_stats", x => new { x.day, x.event_name, x.category_slug, x.region_slug, x.vendor_slug });
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ts = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_name = table.Column<string>(type: "text", nullable: false),
                    session_hash = table.Column<string>(type: "text", nullable: true),
                    page = table.Column<string>(type: "text", nullable: true),
                    props = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sponsorships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsorships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category_slug = table.Column<string>(type: "text", nullable: false),
                    region_slug = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: true),
                    lng = table.Column<double>(type: "double precision", nullable: true),
                    location_precision = table.Column<string>(type: "text", nullable: false),
                    coverage_regions = table.Column<List<string>>(type: "text[]", nullable: false),
                    coverage_all = table.Column<bool>(type: "boolean", nullable: false),
                    coverage_note = table.Column<string>(type: "text", nullable: true),
                    price_kind = table.Column<string>(type: "text", nullable: false),
                    price_from = table.Column<int>(type: "integer", nullable: true),
                    price_to = table.Column<int>(type: "integer", nullable: true),
                    rating = table.Column<double>(type: "double precision", nullable: false),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    rating_source = table.Column<string>(type: "text", nullable: true),
                    verified = table.Column<bool>(type: "boolean", nullable: false),
                    live_calendar = table.Column<bool>(type: "boolean", nullable: false),
                    style_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    about = table.Column<string>(type: "text", nullable: true),
                    services = table.Column<List<string>>(type: "text[]", nullable: false),
                    website = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    social_instagram = table.Column<string>(type: "text", nullable: true),
                    social_facebook = table.Column<string>(type: "text", nullable: true),
                    claim_status = table.Column<string>(type: "text", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    opt_out = table.Column<bool>(type: "boolean", nullable: false),
                    search = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "simple")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "name", "city", "about" }),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "imported_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author = table.Column<string>(type: "text", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imported_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_imported_reviews_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_categories",
                columns: table => new
                {
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_slug = table.Column<string>(type: "text", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_categories", x => new { x.vendor_id, x.category_slug });
                    table.ForeignKey(
                        name: "FK_vendor_categories_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_cover = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_photos", x => x.id);
                    table.ForeignKey(
                        name: "FK_vendor_photos_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_event_name_ts",
                table: "events",
                columns: new[] { "event_name", "ts" });

            migrationBuilder.CreateIndex(
                name: "IX_events_ts",
                table: "events",
                column: "ts");

            migrationBuilder.CreateIndex(
                name: "IX_imported_reviews_vendor_id",
                table: "imported_reviews",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "IX_sponsorships_vendor_id",
                table: "sponsorships",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_categories_category_slug",
                table: "vendor_categories",
                column: "category_slug");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_photos_vendor_id",
                table: "vendor_photos",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendors_category_slug",
                table: "vendors",
                column: "category_slug");

            migrationBuilder.CreateIndex(
                name: "IX_vendors_city",
                table: "vendors",
                column: "city")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_vendors_name",
                table: "vendors",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_vendors_region_slug",
                table: "vendors",
                column: "region_slug");

            migrationBuilder.CreateIndex(
                name: "IX_vendors_search",
                table: "vendors",
                column: "search")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_vendors_slug",
                table: "vendors",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_stats");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "imported_reviews");

            migrationBuilder.DropTable(
                name: "sponsorships");

            migrationBuilder.DropTable(
                name: "vendor_categories");

            migrationBuilder.DropTable(
                name: "vendor_photos");

            migrationBuilder.DropTable(
                name: "vendors");
        }
    }
}
