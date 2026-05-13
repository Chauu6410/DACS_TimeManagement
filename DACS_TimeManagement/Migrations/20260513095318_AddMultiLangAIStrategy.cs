using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiLangAIStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AIStrategy",
                table: "Projects",
                newName: "AIStrategyVi");

            migrationBuilder.AddColumn<string>(
                name: "AIStrategyEn",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AIStrategyEn",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "AIStrategyVi",
                table: "Projects",
                newName: "AIStrategy");
        }
    }
}
