using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IfsaKlasik.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MemberPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nickname = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EnjoyedPlaying = table.Column<bool>(type: "bit", nullable: false),
                    DeveloperMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayFeedbacks_RoomCode",
                table: "PlayFeedbacks",
                column: "RoomCode");

            migrationBuilder.CreateIndex(
                name: "IX_PlayFeedbacks_SubmittedAtUtc",
                table: "PlayFeedbacks",
                column: "SubmittedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayFeedbacks");
        }
    }
}
