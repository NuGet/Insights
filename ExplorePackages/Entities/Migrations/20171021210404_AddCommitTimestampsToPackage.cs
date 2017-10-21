using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddCommitTimestampsToPackage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FirstCommitTimestamp",
                table: "Packages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastCommitTimestamp",
                table: "Packages",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstCommitTimestamp",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "LastCommitTimestamp",
                table: "Packages");
        }
    }
}
