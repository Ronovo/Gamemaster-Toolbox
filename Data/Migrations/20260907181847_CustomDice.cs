using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomDice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiceCount",
                table: "Oracles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiceFaces",
                table: "Oracles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiceCount",
                table: "Oracles");

            migrationBuilder.DropColumn(
                name: "DiceFaces",
                table: "Oracles");
        }
    }
}
