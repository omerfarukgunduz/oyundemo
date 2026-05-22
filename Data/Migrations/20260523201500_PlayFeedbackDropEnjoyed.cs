using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IfsaKlasik.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayFeedbackDropEnjoyed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnjoyedPlaying",
                table: "PlayFeedbacks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnjoyedPlaying",
                table: "PlayFeedbacks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
