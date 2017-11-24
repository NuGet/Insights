using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cursors",
                columns: table => new
                {
                    CursorKey = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursors", x => x.CursorKey);
                });

            migrationBuilder.CreateTable(
                name: "PackageRegistrations",
                columns: table => new
                {
                    PackageRegistrationKey = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageRegistrations", x => x.PackageRegistrationKey);
                });

            migrationBuilder.CreateTable(
                name: "PackageQueries",
                columns: table => new
                {
                    PackageQueryKey = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CursorKey = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageQueries", x => x.PackageQueryKey);
                    table.ForeignKey(
                        name: "FK_PackageQueries_Cursors_CursorKey",
                        column: x => x.CursorKey,
                        principalTable: "Cursors",
                        principalColumn: "CursorKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    PackageKey = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identity = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    PackageRegistrationKey = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.PackageKey);
                    table.ForeignKey(
                        name: "FK_Packages_PackageRegistrations_PackageRegistrationKey",
                        column: x => x.PackageRegistrationKey,
                        principalTable: "PackageRegistrations",
                        principalColumn: "PackageRegistrationKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogPackages",
                columns: table => new
                {
                    PackageKey = table.Column<long>(type: "INTEGER", nullable: false),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstCommitTimestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    LastCommitTimestamp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPackages", x => x.PackageKey);
                    table.ForeignKey(
                        name: "FK_CatalogPackages_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageQueryMatches",
                columns: table => new
                {
                    PackageQueryMatchKey = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageKey = table.Column<long>(type: "INTEGER", nullable: false),
                    PackageQueryKey = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageQueryMatches", x => x.PackageQueryMatchKey);
                    table.ForeignKey(
                        name: "FK_PackageQueryMatches_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageQueryMatches_PackageQueries_PackageQueryKey",
                        column: x => x.PackageQueryKey,
                        principalTable: "PackageQueries",
                        principalColumn: "PackageQueryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "V2Packages",
                columns: table => new
                {
                    PackageKey = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedTimestamp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_V2Packages", x => x.PackageKey);
                    table.ForeignKey(
                        name: "FK_V2Packages_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackages_LastCommitTimestamp",
                table: "CatalogPackages",
                column: "LastCommitTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Cursors_Name",
                table: "Cursors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueries_CursorKey",
                table: "PackageQueries",
                column: "CursorKey");

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueries_Name",
                table: "PackageQueries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueryMatches_PackageKey",
                table: "PackageQueryMatches",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueryMatches_PackageQueryKey_PackageKey",
                table: "PackageQueryMatches",
                columns: new[] { "PackageQueryKey", "PackageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageRegistrations_Id",
                table: "PackageRegistrations",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Identity",
                table: "Packages",
                column: "Identity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PackageRegistrationKey_Version",
                table: "Packages",
                columns: new[] { "PackageRegistrationKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_V2Packages_CreatedTimestamp",
                table: "V2Packages",
                column: "CreatedTimestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogPackages");

            migrationBuilder.DropTable(
                name: "PackageQueryMatches");

            migrationBuilder.DropTable(
                name: "V2Packages");

            migrationBuilder.DropTable(
                name: "PackageQueries");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "Cursors");

            migrationBuilder.DropTable(
                name: "PackageRegistrations");
        }
    }
}
