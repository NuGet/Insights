using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.Sqlite
{
    public partial class IncludeInReverseDependencyIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey_PackageDependencyKey",
                table: "PackageDependencies",
                columns: new[] { "DependencyPackageRegistrationKey", "PackageDependencyKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey_PackageDependencyKey",
                table: "PackageDependencies");
        }
    }
}
