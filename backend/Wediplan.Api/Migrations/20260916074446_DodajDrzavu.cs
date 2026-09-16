using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wediplan.Api.Migrations
{
    /// <inheritdoc />
    public partial class DodajDrzavu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "vendors",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "country",
                table: "vendors");
        }
    }
}
