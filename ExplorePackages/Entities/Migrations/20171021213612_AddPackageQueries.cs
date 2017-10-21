using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddPackageQueries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageQueries",
                columns: table => new
                {
                    PackageQueryKey = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT COLLATE NOCASE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageQueries", x => x.PackageQueryKey);
                });

            migrationBuilder.CreateTable(
                name: "PackageQueryMatches",
                columns: table => new
                {
                    PackageQueryMatchKey = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_PackageQueries_Name",
                table: "PackageQueries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueryMatches_PackageKey",
                table: "PackageQueryMatches",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_PackageQueryMatches_PackageQueryKey",
                table: "PackageQueryMatches",
                column: "PackageQueryKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageQueryMatches");

            migrationBuilder.DropTable(
                name: "PackageQueries");
        }
    }
}
