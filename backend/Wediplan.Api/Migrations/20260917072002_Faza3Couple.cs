using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wediplan.Api.Migrations
{
    /// <inheritdoc />
    public partial class Faza3Couple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_plans",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guests = table.Column<int>(type: "integer", nullable: false),
                    region = table.Column<string>(type: "text", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_plans", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_budget_plans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorites", x => x.id);
                    table.ForeignKey(
                        name: "FK_favorites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_favorites_user_id",
                table: "favorites",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_favorites_user_id_vendor_id",
                table: "favorites",
                columns: new[] { "user_id", "vendor_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_plans");

            migrationBuilder.DropTable(
                name: "favorites");
        }
    }
}
