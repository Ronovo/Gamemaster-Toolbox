using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class Ownership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Everything from before accounts stays visible to everyone, with no owner.
            migrationBuilder.DropIndex(
                name: "IX_GameSystems_Name",
                table: "GameSystems");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Oracles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "Oracles",
                type: "TEXT",
                nullable: false,
                defaultValue: "Everyone");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "GameSystems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "GameSystems",
                type: "TEXT",
                nullable: false,
                defaultValue: "Everyone");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Flows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "Flows",
                type: "TEXT",
                nullable: false,
                defaultValue: "Everyone");

            migrationBuilder.CreateIndex(
                name: "IX_GameSystems_OwnerId_Name",
                table: "GameSystems",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameSystems_OwnerId_Name",
                table: "GameSystems");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Oracles");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Oracles");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "GameSystems");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "GameSystems");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Flows");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Flows");

            migrationBuilder.CreateIndex(
                name: "IX_GameSystems_Name",
                table: "GameSystems",
                column: "Name",
                unique: true);
        }
    }
}
