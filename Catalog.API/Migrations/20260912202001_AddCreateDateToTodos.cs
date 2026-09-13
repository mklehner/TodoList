using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCreateDateToTodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Todos",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Todos");
        }
    }
}
