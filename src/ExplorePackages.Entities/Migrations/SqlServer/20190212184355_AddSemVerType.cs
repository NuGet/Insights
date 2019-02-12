using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.SqlServer
{
    public partial class AddSemVerType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages");

            migrationBuilder.DropColumn(
                name: "SemVer2",
                table: "CatalogPackages");

            migrationBuilder.DropColumn(
                name: "IsSemVer2",
                table: "CatalogLeaves");

            migrationBuilder.AddColumn<int>(
                name: "SemVerType",
                table: "CatalogPackages",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SemVerType",
                table: "CatalogLeaves",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages",
                column: "LastCommitTimestamp")
                .Annotation("SqlServer:Include", new[] { "Deleted", "FirstCommitTimestamp", "Listed", "SemVerType" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages");

            migrationBuilder.DropColumn(
                name: "SemVerType",
                table: "CatalogPackages");

            migrationBuilder.DropColumn(
                name: "SemVerType",
                table: "CatalogLeaves");

            migrationBuilder.AddColumn<bool>(
                name: "SemVer2",
                table: "CatalogPackages",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSemVer2",
                table: "CatalogLeaves",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages",
                column: "LastCommitTimestamp")
                .Annotation("SqlServer:Include", new[] { "Deleted", "FirstCommitTimestamp", "Listed", "SemVer2" });
        }
    }
}
