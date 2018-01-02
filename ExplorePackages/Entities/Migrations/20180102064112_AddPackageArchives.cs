using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddPackageArchives : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageArchives",
                columns: table => new
                {
                    PackageKey = table.Column<long>(nullable: false),
                    EntryCount = table.Column<int>(nullable: false),
                    OffsetOfCentralDirectory = table.Column<uint>(nullable: false),
                    Size = table.Column<long>(nullable: false),
                    Zip64OffsetOfCentralDirectory = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageArchives", x => x.PackageKey);
                    table.ForeignKey(
                        name: "FK_PackageArchives_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageArchives");
        }
    }
}
