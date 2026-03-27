using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebLinhKienPc.Migrations
{
    /// <inheritdoc />
    public partial class addlogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "SiteInfos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "SiteInfos");
        }
    }
}
