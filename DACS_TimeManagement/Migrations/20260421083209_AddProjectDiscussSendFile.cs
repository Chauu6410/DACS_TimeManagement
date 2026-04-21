using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_TimeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDiscussSendFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "ProjectDiscussion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "ProjectDiscussion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentOriginalName",
                table: "ProjectDiscussion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSize",
                table: "ProjectDiscussion",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "ProjectDiscussion");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "ProjectDiscussion");

            migrationBuilder.DropColumn(
                name: "AttachmentOriginalName",
                table: "ProjectDiscussion");

            migrationBuilder.DropColumn(
                name: "AttachmentSize",
                table: "ProjectDiscussion");
        }
    }
}
