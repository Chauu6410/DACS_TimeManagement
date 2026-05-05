using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "UserProfiles");
        }
    }
}
