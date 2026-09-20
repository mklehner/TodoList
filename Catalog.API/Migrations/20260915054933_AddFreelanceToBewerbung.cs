using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFreelanceToBewerbung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Anschreiben",
                table: "Bewerbungen",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreelance",
                table: "Bewerbungen",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Anschreiben",
                table: "Bewerbungen");

            migrationBuilder.DropColumn(
                name: "IsFreelance",
                table: "Bewerbungen");
        }
    }
}
