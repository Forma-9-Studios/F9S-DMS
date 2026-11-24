using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F9SDMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotosAssistAndEarlyClockIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssistedUserId",
                table: "ProjectTimeEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CountFrom",
                table: "AttendanceSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EarlyDecidedAt",
                table: "AttendanceSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlyDecidedById",
                table: "AttendanceSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlyStatus",
                table: "AttendanceSessions",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssistNote",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssistingUserId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhotoUpdatedAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssistedUserId",
                table: "ProjectTimeEntries");

            migrationBuilder.DropColumn(
                name: "CountFrom",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "EarlyDecidedAt",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "EarlyDecidedById",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "EarlyStatus",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "AssistNote",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssistingUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PhotoUpdatedAt",
                table: "AspNetUsers");
        }
    }
}
