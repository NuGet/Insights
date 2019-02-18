using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.Sqlite
{
    public partial class AddReverseDependencyIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey",
                table: "PackageDependencies");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey_ParentPackageKey",
                table: "PackageDependencies",
                columns: new[] { "DependencyPackageRegistrationKey", "ParentPackageKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey_ParentPackageKey",
                table: "PackageDependencies");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey",
                table: "PackageDependencies",
                column: "DependencyPackageRegistrationKey");
        }
    }
}
