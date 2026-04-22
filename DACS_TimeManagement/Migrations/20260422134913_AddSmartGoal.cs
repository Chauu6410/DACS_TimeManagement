using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentStreak",
                table: "PersonalGoals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "PersonalGoals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "PersonalGoals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStreak",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "PersonalGoals");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "PersonalGoals");
        }
    }
}
