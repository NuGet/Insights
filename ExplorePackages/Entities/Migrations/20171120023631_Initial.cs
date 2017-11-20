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
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursors", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    PackageKey = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstCommitTimestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Id = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    Identity = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false),
                    LastCommitTimestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.PackageKey);
                });

            migrationBuilder.CreateTable(
                name: "PackageQueries",
                columns: table => new
                {
                    PackageQueryKey = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CursorName = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageQueries", x => x.PackageQueryKey);
                    table.ForeignKey(
                        name: "FK_PackageQueries_Cursors_CursorName",
                        column: x => x.CursorName,
                        principalTable: "Cursors",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageQueryMatches",
                columns: table => new
                {
                    PackageQueryMatchKey = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PackageQueryKey = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueries_CursorName",
                table: "PackageQueries",
                column: "CursorName");

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
                name: "IX_Packages_Identity",
                table: "Packages",
                column: "Identity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_LastCommitTimestamp",
                table: "Packages",
                column: "LastCommitTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Id_Version",
                table: "Packages",
                columns: new[] { "Id", "Version" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageQueryMatches");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "PackageQueries");

            migrationBuilder.DropTable(
                name: "Cursors");
        }
    }
}
