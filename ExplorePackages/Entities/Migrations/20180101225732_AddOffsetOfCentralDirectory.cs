using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddOffsetOfCentralDirectory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OffsetOfCentralDirectory",
                table: "PackageArchives",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Zip64OffsetOfCentralDirectory",
                table: "PackageArchives",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OffsetOfCentralDirectory",
                table: "PackageArchives");

            migrationBuilder.DropColumn(
                name: "Zip64OffsetOfCentralDirectory",
                table: "PackageArchives");
        }
    }
}
