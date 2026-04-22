using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionToPersonalGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PersonalGoals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "PersonalGoals");
        }
    }
}
