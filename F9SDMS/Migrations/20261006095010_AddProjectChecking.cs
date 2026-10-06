using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F9SDMS.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectChecking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkType",
                table: "ProjectTimeEntries",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Modeling");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProjectSubcategories",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "In Progress");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ProjectAssignments",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Engineer");

            migrationBuilder.AddColumn<int>(
                name: "CurrentCheckId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubcategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedById = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PdfFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    PdfStoredName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PdfSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CheckerId = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ResultAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    MarkupFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    MarkupStoredName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MarkupSize = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckSubmissions_ProjectSubcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "ProjectSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CheckSubmissions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckSubmissions_CheckerId",
                table: "CheckSubmissions",
                column: "CheckerId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckSubmissions_ProjectId_SubcategoryId",
                table: "CheckSubmissions",
                columns: new[] { "ProjectId", "SubcategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckSubmissions_SubcategoryId",
                table: "CheckSubmissions",
                column: "SubcategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckSubmissions");

            migrationBuilder.DropColumn(
                name: "WorkType",
                table: "ProjectTimeEntries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectSubcategories");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProjectAssignments");

            migrationBuilder.DropColumn(
                name: "CurrentCheckId",
                table: "AspNetUsers");
        }
    }
}
