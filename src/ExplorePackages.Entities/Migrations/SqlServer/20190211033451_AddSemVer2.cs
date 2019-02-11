using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.SqlServer
{
    public partial class AddSemVer2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages");

            migrationBuilder.AlterColumn<bool>(
                name: "Listed",
                table: "CatalogPackages",
                nullable: true,
                oldClrType: typeof(bool));

            migrationBuilder.AddColumn<bool>(
                name: "SemVer2",
                table: "CatalogPackages",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsListed",
                table: "CatalogLeaves",
                nullable: true,
                oldClrType: typeof(bool));

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

        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<bool>(
                name: "Listed",
                table: "CatalogPackages",
                nullable: false,
                oldClrType: typeof(bool),
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsListed",
                table: "CatalogLeaves",
                nullable: false,
                oldClrType: typeof(bool),
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages",
                column: "LastCommitTimestamp")
                .Annotation("SqlServer:Include", new[] { "Deleted", "FirstCommitTimestamp", "Listed" });
        }
    }
}
