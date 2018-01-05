using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddPackageEntries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageEntries",
                columns: table => new
                {
                    PackageEntryKey = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Comment = table.Column<byte[]>(nullable: false),
                    CommentSize = table.Column<ushort>(nullable: false),
                    CompressedSize = table.Column<uint>(nullable: false),
                    CompressionMethod = table.Column<ushort>(nullable: false),
                    Crc32 = table.Column<uint>(nullable: false),
                    DiskNumberStart = table.Column<ushort>(nullable: false),
                    ExternalAttributes = table.Column<uint>(nullable: false),
                    ExtraField = table.Column<byte[]>(nullable: false),
                    ExtraFieldSize = table.Column<ushort>(nullable: false),
                    Flags = table.Column<ushort>(nullable: false),
                    Index = table.Column<ulong>(nullable: false),
                    InternalAttributes = table.Column<ushort>(nullable: false),
                    LastModifiedDate = table.Column<ushort>(nullable: false),
                    LastModifiedTime = table.Column<ushort>(nullable: false),
                    LocalHeaderOffset = table.Column<uint>(nullable: false),
                    Name = table.Column<byte[]>(nullable: false),
                    NameSize = table.Column<ushort>(nullable: false),
                    PackageKey = table.Column<long>(nullable: false),
                    UncompressedSize = table.Column<uint>(nullable: false),
                    VersionMadeBy = table.Column<ushort>(nullable: false),
                    VersionToExtract = table.Column<ushort>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageEntries", x => x.PackageEntryKey);
                    table.ForeignKey(
                        name: "FK_PackageEntries_PackageArchives_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "PackageArchives",
                        principalColumn: "PackageKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageEntries_PackageKey_Index",
                table: "PackageEntries",
                columns: new[] { "PackageKey", "Index" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageEntries");
        }
    }
}
