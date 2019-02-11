using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.Sqlite
{
    public partial class AddCommitCollectorProgressTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommitCollectorProgressTokens",
                columns: table => new
                {
                    CommitCollectorProgressTokenKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: false),
                    FirstCommitTimestamp = table.Column<long>(nullable: false),
                    LastCommitTimestamp = table.Column<long>(nullable: false),
                    SerializedProgressToken = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitCollectorProgressTokens", x => x.CommitCollectorProgressTokenKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommitCollectorProgressTokens_Name",
                table: "CommitCollectorProgressTokens",
                column: "Name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitCollectorProgressTokens");
        }
    }
}
