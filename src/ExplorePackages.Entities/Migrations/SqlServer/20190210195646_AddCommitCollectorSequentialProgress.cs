using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.SqlServer
{
    public partial class AddCommitCollectorSequentialProgress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommitCollectorSequentialProgress",
                columns: table => new
                {
                    CommitCollectorSequentialProgressKey = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: false),
                    FirstCommitTimestamp = table.Column<long>(nullable: false),
                    LastCommitTimestamp = table.Column<long>(nullable: false),
                    Skip = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitCollectorSequentialProgress", x => x.CommitCollectorSequentialProgressKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommitCollectorSequentialProgress_Name",
                table: "CommitCollectorSequentialProgress",
                column: "Name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitCollectorSequentialProgress");
        }
    }
}
