using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddPackageDependencies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Frameworks",
                columns: table => new
                {
                    FrameworkKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalValue = table.Column<string>(nullable: false),
                    Value = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Frameworks", x => x.FrameworkKey);
                });

            migrationBuilder.CreateTable(
                name: "PackageDependencies",
                columns: table => new
                {
                    PackageDependencyKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DependencyPackageRegistrationKey = table.Column<long>(nullable: false),
                    FrameworkKey = table.Column<long>(nullable: true),
                    MinimumDependencyPackageKey = table.Column<long>(nullable: true),
                    OriginalVersionRange = table.Column<string>(nullable: true),
                    ParentPackageKey = table.Column<long>(nullable: false),
                    VersionRange = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDependencies", x => x.PackageDependencyKey);
                    table.ForeignKey(
                        name: "FK_PackageDependencies_PackageRegistrations_DependencyPackageRegistrationKey",
                        column: x => x.DependencyPackageRegistrationKey,
                        principalTable: "PackageRegistrations",
                        principalColumn: "PackageRegistrationKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageDependencies_Frameworks_FrameworkKey",
                        column: x => x.FrameworkKey,
                        principalTable: "Frameworks",
                        principalColumn: "FrameworkKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageDependencies_Packages_MinimumDependencyPackageKey",
                        column: x => x.MinimumDependencyPackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageDependencies_Packages_ParentPackageKey",
                        column: x => x.ParentPackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Frameworks_OriginalValue",
                table: "Frameworks",
                column: "OriginalValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_DependencyPackageRegistrationKey",
                table: "PackageDependencies",
                column: "DependencyPackageRegistrationKey");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_FrameworkKey",
                table: "PackageDependencies",
                column: "FrameworkKey");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_MinimumDependencyPackageKey",
                table: "PackageDependencies",
                column: "MinimumDependencyPackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_ParentPackageKey_DependencyPackageRegistrationKey_FrameworkKey",
                table: "PackageDependencies",
                columns: new[] { "ParentPackageKey", "DependencyPackageRegistrationKey", "FrameworkKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDependencies");

            migrationBuilder.DropTable(
                name: "Frameworks");
        }
    }
}
