using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskChangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    RequesterId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChangeRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskChangeRequests");
        }
    }
}
