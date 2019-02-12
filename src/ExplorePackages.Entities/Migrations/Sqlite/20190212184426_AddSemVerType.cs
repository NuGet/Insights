using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.Sqlite
{
    public partial class AddSemVerType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
