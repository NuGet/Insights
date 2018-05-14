using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddCatalogEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogPages",
                columns: table => new
                {
                    CatalogPageKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPages", x => x.CatalogPageKey);
                });

            migrationBuilder.CreateTable(
                name: "CatalogCommits",
                columns: table => new
                {
                    CatalogCommitKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CatalogPageKey = table.Column<long>(nullable: false),
                    CommitId = table.Column<string>(nullable: false),
                    CommitTimestamp = table.Column<long>(nullable: false),
                    Count = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogCommits", x => x.CatalogCommitKey);
                    table.ForeignKey(
                        name: "FK_CatalogCommits_CatalogPages_CatalogPageKey",
                        column: x => x.CatalogPageKey,
                        principalTable: "CatalogPages",
                        principalColumn: "CatalogPageKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogLeaves",
                columns: table => new
                {
                    CatalogLeafKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CatalogCommitKey = table.Column<long>(nullable: false),
                    PackageKey = table.Column<long>(nullable: false),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogLeaves", x => x.CatalogLeafKey);
                    table.ForeignKey(
                        name: "FK_CatalogLeaves_CatalogCommits_CatalogCommitKey",
                        column: x => x.CatalogCommitKey,
                        principalTable: "CatalogCommits",
                        principalColumn: "CatalogCommitKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogLeaves_CatalogPackages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "CatalogPackages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCommits_CatalogPageKey",
                table: "CatalogCommits",
                column: "CatalogPageKey");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCommits_CommitId",
                table: "CatalogCommits",
                column: "CommitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCommits_CommitTimestamp",
                table: "CatalogCommits",
                column: "CommitTimestamp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogLeaves_CatalogCommitKey",
                table: "CatalogLeaves",
                column: "CatalogCommitKey");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogLeaves_PackageKey",
                table: "CatalogLeaves",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPages_Url",
                table: "CatalogPages",
                column: "Url",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogLeaves");

            migrationBuilder.DropTable(
                name: "CatalogCommits");

            migrationBuilder.DropTable(
                name: "CatalogPages");
        }
    }
}
