using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wediplan.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClaimVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_verification_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_verification_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_claim_verification_tokens_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_claim_verification_tokens_claim_id",
                table: "claim_verification_tokens",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "IX_claim_verification_tokens_token_hash",
                table: "claim_verification_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_verification_tokens");
        }
    }
}
