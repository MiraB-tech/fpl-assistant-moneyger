using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistantMoneyger.Api.Migrations
{
    /// <inheritdoc />
    public partial class RedesignPredictionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "XaXgPer90",
                table: "Predictions",
                newName: "StartProbability");

            migrationBuilder.RenameColumn(
                name: "RecentForm",
                table: "Predictions",
                newName: "ExpectedGoals");

            migrationBuilder.RenameColumn(
                name: "MinutesReliability",
                table: "Predictions",
                newName: "ExpectedAssists");

            migrationBuilder.RenameColumn(
                name: "FixtureDifficulty",
                table: "Predictions",
                newName: "CleanSheetProbability");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartProbability",
                table: "Predictions",
                newName: "XaXgPer90");

            migrationBuilder.RenameColumn(
                name: "ExpectedGoals",
                table: "Predictions",
                newName: "RecentForm");

            migrationBuilder.RenameColumn(
                name: "ExpectedAssists",
                table: "Predictions",
                newName: "MinutesReliability");

            migrationBuilder.RenameColumn(
                name: "CleanSheetProbability",
                table: "Predictions",
                newName: "FixtureDifficulty");
        }
    }
}
