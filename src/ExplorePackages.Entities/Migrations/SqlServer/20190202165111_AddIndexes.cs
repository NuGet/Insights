using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.SqlServer
{
    public partial class AddIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_ParentPackageKey",
                table: "PackageDependencies",
                column: "ParentPackageKey")
                .Annotation("SqlServer:Include", new[] { "BestDependencyPackageKey", "DependencyPackageRegistrationKey", "FrameworkKey", "MinimumDependencyPackageKey", "OriginalVersionRange", "VersionRange" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages",
                column: "LastCommitTimestamp")
                .Annotation("SqlServer:Include", new[] { "Deleted", "FirstCommitTimestamp", "Listed" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageDependencies_ParentPackageKey",
                table: "PackageDependencies");

            migrationBuilder.DropIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages",
                column: "LastCommitTimestamp");
        }
    }
}
