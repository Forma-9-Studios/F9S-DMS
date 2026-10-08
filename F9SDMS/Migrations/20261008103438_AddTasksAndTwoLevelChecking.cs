using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F9SDMS.Migrations
{
    /// <inheritdoc />
    public partial class AddTasksAndTwoLevelChecking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaskId",
                table: "ProjectTimeEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "CheckSubmissions",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskId",
                table: "CheckSubmissions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentTaskId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReviewerId = table.Column<string>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    FileStoredName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckReviews_CheckSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "CheckSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubcategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, defaultValue: "In Progress"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_ProjectSubcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "ProjectSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimeEntries_TaskId",
                table: "ProjectTimeEntries",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckSubmissions_TaskId",
                table: "CheckSubmissions",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckReviews_SubmissionId",
                table: "CheckReviews",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId",
                table: "ProjectTasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_SubcategoryId",
                table: "ProjectTasks",
                column: "SubcategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckSubmissions_ProjectTasks_TaskId",
                table: "CheckSubmissions",
                column: "TaskId",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTimeEntries_ProjectTasks_TaskId",
                table: "ProjectTimeEntries",
                column: "TaskId",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckSubmissions_ProjectTasks_TaskId",
                table: "CheckSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTimeEntries_ProjectTasks_TaskId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropTable(
                name: "CheckReviews");

            migrationBuilder.DropTable(
                name: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTimeEntries_TaskId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_CheckSubmissions_TaskId",
                table: "CheckSubmissions");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "CheckSubmissions");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "CheckSubmissions");

            migrationBuilder.DropColumn(
                name: "CurrentTaskId",
                table: "AspNetUsers");
        }
    }
}
