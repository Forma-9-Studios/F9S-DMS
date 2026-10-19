using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F9SDMS.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "ProjectTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Link = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignees_ProjectTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TaskId",
                table: "Notifications",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_EmployeeId",
                table: "TaskAssignees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_TaskId_EmployeeId",
                table: "TaskAssignees",
                columns: new[] { "TaskId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "TaskAssignees");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "ProjectTasks");
        }
    }
}
