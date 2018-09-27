using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities.Migrations
{
    public partial class AddIsListedToCatalogLeaf : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsListed",
                table: "CatalogLeaves",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsListed",
                table: "CatalogLeaves");
        }
    }
}
