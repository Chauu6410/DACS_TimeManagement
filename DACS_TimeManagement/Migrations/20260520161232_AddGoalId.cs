using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoalId",
                table: "TimeLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFocusSession",
                table: "TimeLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_GoalId",
                table: "TimeLogs",
                column: "GoalId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeLogs_PersonalGoals_GoalId",
                table: "TimeLogs",
                column: "GoalId",
                principalTable: "PersonalGoals",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeLogs_PersonalGoals_GoalId",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_GoalId",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "GoalId",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "IsFocusSession",
                table: "TimeLogs");
        }
    }
}
