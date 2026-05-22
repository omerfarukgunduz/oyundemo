using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IfsaKlasik.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GameFinishTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GameStartedAtUtc",
                table: "Rooms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "RoundAnswers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ra
                SET SubmittedAtUtc = r.StartedAtUtc
                FROM RoundAnswers ra
                INNER JOIN Rounds r ON r.Id = ra.RoundId
                WHERE ra.SubmittedAtUtc IS NULL
                """);

            migrationBuilder.Sql(
                """
                UPDATE Rooms
                SET GameStartedAtUtc = (
                    SELECT MIN(r.StartedAtUtc)
                    FROM Rounds r
                    WHERE r.RoomId = Rooms.Id)
                WHERE GameStartedAtUtc IS NULL AND Phase <> 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "RoundAnswers");

            migrationBuilder.DropColumn(
                name: "GameStartedAtUtc",
                table: "Rooms");
        }
    }
}
