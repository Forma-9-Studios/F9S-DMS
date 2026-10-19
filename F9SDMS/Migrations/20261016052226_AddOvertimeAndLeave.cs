using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F9SDMS.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeAndLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOvertime",
                table: "AttendanceSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LimitMinutes",
                table: "AttendanceSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaveAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaveType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Days = table.Column<double>(type: "REAL", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeavePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VacationDaysPerYear = table.Column<double>(type: "REAL", nullable: false),
                    SickDaysPerYear = table.Column<double>(type: "REAL", nullable: false),
                    VacationNoticeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CarryOverMaxDays = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<string>(type: "TEXT", nullable: false),
                    LeaveType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsHalfDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    HalfDayPart = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    TotalDays = table.Column<double>(type: "REAL", nullable: false),
                    CreditDays = table.Column<double>(type: "REAL", nullable: false),
                    UnpaidDays = table.Column<double>(type: "REAL", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecidedById = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DecisionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OvertimeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecidedById = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DecisionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "LeavePolicies",
                columns: new[] { "Id", "CarryOverMaxDays", "SickDaysPerYear", "VacationDaysPerYear", "VacationNoticeDays" },
                values: new object[] { 1, 5.0, 10.0, 15.0, 3 });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveAdjustments_EmployeeId_Year",
                table: "LeaveAdjustments",
                columns: new[] { "EmployeeId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_StartDate",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_Status",
                table: "LeaveRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeId_Date",
                table: "OvertimeRequests",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ProjectId",
                table: "OvertimeRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_Status",
                table: "OvertimeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveAdjustments");

            migrationBuilder.DropTable(
                name: "LeavePolicies");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "IsOvertime",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "LimitMinutes",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "AspNetUsers");
        }
    }
}
