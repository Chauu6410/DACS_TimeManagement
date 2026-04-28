using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class Goal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoalName",
                table: "PersonalGoals");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PersonalGoals",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<double>(
                name: "CompletedHours",
                table: "PersonalGoals",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "CompletedTasks",
                table: "PersonalGoals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PersonalGoals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PersonalGoals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "TargetHours",
                table: "PersonalGoals",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetTasks",
                table: "PersonalGoals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PersonalGoals",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "PersonalGoals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PersonalGoals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoalProgressHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Progress = table.Column<double>(type: "float", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalProgressHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalProgressHistories_PersonalGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "PersonalGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalTasks",
                columns: table => new
                {
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    WorkTaskId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalTasks", x => new { x.GoalId, x.WorkTaskId });
                    table.ForeignKey(
                        name: "FK_GoalTasks_PersonalGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "PersonalGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalTasks_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalGoals_UserId_Status",
                table: "PersonalGoals",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressHistories_GoalId",
                table: "GoalProgressHistories",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalTasks_WorkTaskId",
                table: "GoalTasks",
                column: "WorkTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalProgressHistories");

            migrationBuilder.DropTable(
                name: "GoalTasks");

            migrationBuilder.DropIndex(
                name: "IX_PersonalGoals_UserId_Status",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "CompletedHours",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "CompletedTasks",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "TargetHours",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "TargetTasks",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PersonalGoals");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PersonalGoals",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "GoalName",
                table: "PersonalGoals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
