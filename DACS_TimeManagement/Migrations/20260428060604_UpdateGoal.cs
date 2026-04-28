using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "PersonalGoals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalGoals_ProjectId",
                table: "PersonalGoals",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalGoals_Projects_ProjectId",
                table: "PersonalGoals",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonalGoals_Projects_ProjectId",
                table: "PersonalGoals");

            migrationBuilder.DropIndex(
                name: "IX_PersonalGoals_ProjectId",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PersonalGoals");
        }
    }
}
