using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddPackageDownloads : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageDownloads",
                columns: table => new
                {
                    PackageKey = table.Column<long>(type: "INTEGER", nullable: false),
                    Downloads = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDownloads", x => x.PackageKey);
                    table.ForeignKey(
                        name: "FK_PackageDownloads_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDownloads");
        }
    }
}
