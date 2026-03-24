using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebLinhKienPc.Migrations
{
    /// <inheritdoc />
    public partial class addSold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Sold",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sold",
                table: "Products");
        }
    }
}
