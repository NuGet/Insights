using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.SqlServer
{
    public partial class IncludeInReverseDependencyIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey_PackageDependencyKey",
                table: "PackageDependencies",
                columns: new[] { "DependencyPackageRegistrationKey", "PackageDependencyKey" })
                .Annotation("SqlServer:Include", new[] { "BestDependencyPackageKey", "FrameworkKey", "MinimumDependencyPackageKey", "OriginalVersionRange", "ParentPackageKey", "VersionRange" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey_PackageDependencyKey",
                table: "PackageDependencies");
        }
    }
}
