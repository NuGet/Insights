using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddPackageArchives : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageArchives",
                columns: table => new
                {
                    PackageKey = table.Column<long>(nullable: false),
                    CentralDirectorySize = table.Column<uint>(nullable: false),
                    Comment = table.Column<byte[]>(nullable: false),
                    CommentSize = table.Column<ushort>(nullable: false),
                    DiskWithStartOfCentralDirectory = table.Column<ushort>(nullable: false),
                    EntriesForWholeCentralDirectory = table.Column<ushort>(nullable: false),
                    EntriesInThisDisk = table.Column<ushort>(nullable: false),
                    EntryCount = table.Column<int>(nullable: false),
                    NumberOfThisDisk = table.Column<ushort>(nullable: false),
                    OffsetAfterEndOfCentralDirectory = table.Column<long>(nullable: false),
                    OffsetOfCentralDirectory = table.Column<uint>(nullable: false),
                    Size = table.Column<long>(nullable: false),
                    Zip64CentralDirectorySize = table.Column<ulong>(nullable: true),
                    Zip64DiskWithStartOfCentralDirectory = table.Column<uint>(nullable: true),
                    Zip64DiskWithStartOfEndOfCentralDirectory = table.Column<uint>(nullable: true),
                    Zip64EndOfCentralDirectoryOffset = table.Column<ulong>(nullable: true),
                    Zip64EntriesForWholeCentralDirectory = table.Column<ulong>(nullable: true),
                    Zip64EntriesInThisDisk = table.Column<ulong>(nullable: true),
                    Zip64NumberOfThisDisk = table.Column<uint>(nullable: true),
                    Zip64OffsetAfterEndOfCentralDirectoryLocator = table.Column<long>(nullable: true),
                    Zip64OffsetOfCentralDirectory = table.Column<ulong>(nullable: true),
                    Zip64SizeOfCentralDirectoryRecord = table.Column<ulong>(nullable: true),
                    Zip64TotalNumberOfDisks = table.Column<uint>(nullable: true),
                    Zip64VersionMadeBy = table.Column<ushort>(nullable: true),
                    Zip64VersionToExtract = table.Column<ushort>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageArchives", x => x.PackageKey);
                    table.ForeignKey(
                        name: "FK_PackageArchives_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageArchives");
        }
    }
}
