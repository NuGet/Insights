using Microsoft.EntityFrameworkCore.Migrations;

namespace Knapcode.ExplorePackages.Entities.Migrations.Sqlite
{
    public partial class LeaseRowVersionTriggers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TRIGGER Leases_RowVersion_Insert
AFTER INSERT ON Leases
BEGIN
    UPDATE Leases SET RowVersion = STRFTIME('%Y-%m-%dT%H:%M:%S.%fZ', 'NOW') WHERE LeaseKey = NEW.LeaseKey;
END;

CREATE TRIGGER Leases_RowVersion_Update
AFTER UPDATE ON Leases
BEGIN
    UPDATE Leases SET RowVersion = STRFTIME('%Y-%m-%dT%H:%M:%S.%fZ', 'NOW') WHERE LeaseKey = NEW.LeaseKey;
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS Leases_RowVersion_Insert;
DROP TRIGGER IF EXISTS Leases_RowVersion_Update;
");
        }
    }
}
