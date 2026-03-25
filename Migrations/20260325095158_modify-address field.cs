using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebLinhKienPc.Migrations
{
    /// <inheritdoc />
    public partial class modifyaddressfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StreetNumber",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StreetNumber",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "Addresses");
        }
    }
}
