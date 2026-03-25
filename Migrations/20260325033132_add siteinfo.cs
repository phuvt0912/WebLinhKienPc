using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebLinhKienPc.Migrations
{
    /// <inheritdoc />
    public partial class addsiteinfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteURL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slogan = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Branch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteInfoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_SiteInfos_SiteInfoId",
                        column: x => x.SiteInfoId,
                        principalTable: "SiteInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartDay = table.Column<int>(type: "int", nullable: false),
                    EndDay = table.Column<int>(type: "int", nullable: false),
                    OpenHour = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseHour = table.Column<TimeSpan>(type: "time", nullable: false),
                    SiteInfoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkHours_SiteInfos_SiteInfoId",
                        column: x => x.SiteInfoId,
                        principalTable: "SiteInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SiteInfoId",
                table: "Addresses",
                column: "SiteInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkHours_SiteInfoId",
                table: "WorkHours",
                column: "SiteInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "WorkHours");

            migrationBuilder.DropTable(
                name: "SiteInfos");
        }
    }
}
