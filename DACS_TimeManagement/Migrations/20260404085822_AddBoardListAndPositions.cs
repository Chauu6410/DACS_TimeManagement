using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardListAndPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropColumn(
            //    name: "DueDate",
            //    table: "WorkTasks");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "WorkTasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "BoardListId",
                table: "WorkTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "WorkTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BoardLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardLists_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_BoardListId",
                table: "WorkTasks",
                column: "BoardListId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardLists_ProjectId",
                table: "BoardLists",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_BoardLists_BoardListId",
                table: "WorkTasks",
                column: "BoardListId",
                principalTable: "BoardLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_BoardLists_BoardListId",
                table: "WorkTasks");

            migrationBuilder.DropTable(
                name: "BoardLists");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_BoardListId",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "BoardListId",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "WorkTasks");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "WorkTasks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "WorkTasks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
