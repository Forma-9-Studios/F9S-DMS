using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F9SDMS.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubcategoryId",
                table: "ProjectTimeEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentSubcategoryId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectSubcategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSubcategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSubcategories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimeEntries_SubcategoryId",
                table: "ProjectTimeEntries",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubcategories_ProjectId",
                table: "ProjectSubcategories",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTimeEntries_ProjectSubcategories_SubcategoryId",
                table: "ProjectTimeEntries",
                column: "SubcategoryId",
                principalTable: "ProjectSubcategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTimeEntries_ProjectSubcategories_SubcategoryId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropTable(
                name: "ProjectSubcategories");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTimeEntries_SubcategoryId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropColumn(
                name: "SubcategoryId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropColumn(
                name: "CurrentSubcategoryId",
                table: "AspNetUsers");
        }
    }
}
