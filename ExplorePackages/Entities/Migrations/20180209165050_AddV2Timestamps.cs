using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddV2Timestamps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastEditedTimestamp",
                table: "V2Packages",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastUpdatedTimestamp",
                table: "V2Packages",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "Listed",
                table: "V2Packages",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PublishedTimestamp",
                table: "V2Packages",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_V2Packages_LastEditedTimestamp",
                table: "V2Packages",
                column: "LastEditedTimestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_V2Packages_LastEditedTimestamp",
                table: "V2Packages");

            migrationBuilder.DropColumn(
                name: "LastEditedTimestamp",
                table: "V2Packages");

            migrationBuilder.DropColumn(
                name: "LastUpdatedTimestamp",
                table: "V2Packages");

            migrationBuilder.DropColumn(
                name: "Listed",
                table: "V2Packages");

            migrationBuilder.DropColumn(
                name: "PublishedTimestamp",
                table: "V2Packages");
        }
    }
}
