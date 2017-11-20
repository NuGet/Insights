using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddV2FeedPackageEntity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "V2PackageEntities",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Created = table.Column<long>(type: "INTEGER", nullable: false),
                    Id = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Identity = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Version = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V2PackageEntities", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_V2PackageEntities_Created",
                table: "V2PackageEntities",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_V2PackageEntities_Id_Version",
                table: "V2PackageEntities",
                columns: new[] { "Id", "Version" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "V2PackageEntities");
        }
    }
}
