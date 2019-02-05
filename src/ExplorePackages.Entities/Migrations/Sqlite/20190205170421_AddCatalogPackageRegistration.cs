using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.Sqlite
{
    public partial class AddCatalogPackageRegistration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogPackageRegistrations",
                columns: table => new
                {
                    PackageRegistrationKey = table.Column<long>(nullable: false),
                    FirstCommitTimestamp = table.Column<long>(nullable: false),
                    LastCommitTimestamp = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPackageRegistrations", x => x.PackageRegistrationKey);
                    table.ForeignKey(
                        name: "FK_CatalogPackageRegistrations_PackageRegistrations_PackageRegistrationKey",
                        column: x => x.PackageRegistrationKey,
                        principalTable: "PackageRegistrations",
                        principalColumn: "PackageRegistrationKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPackageRegistrations_LastCommitTimestamp",
                table: "CatalogPackageRegistrations",
                column: "LastCommitTimestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogPackageRegistrations");
        }
    }
}
