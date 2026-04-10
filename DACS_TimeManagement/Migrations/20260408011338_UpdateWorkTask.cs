using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWorkTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "WorkTasks",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AssigneeId",
                table: "WorkTasks",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_AssigneeId",
                table: "WorkTasks",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_UserId",
                table: "WorkTasks",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_AspNetUsers_AssigneeId",
                table: "WorkTasks",
                column: "AssigneeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_AspNetUsers_UserId",
                table: "WorkTasks",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_AspNetUsers_AssigneeId",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_AspNetUsers_UserId",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_AssigneeId",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_UserId",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Notifications");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "WorkTasks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "AssigneeId",
                table: "WorkTasks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
