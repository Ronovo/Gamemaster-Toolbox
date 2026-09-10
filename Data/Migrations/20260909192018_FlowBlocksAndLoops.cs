using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlowBlocksAndLoops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlockEndSortOrder",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountDice",
                table: "FlowSteps",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountMode",
                table: "FlowSteps",
                type: "TEXT",
                nullable: false,
                defaultValue: "Preset");

            migrationBuilder.AddColumn<int>(
                name: "BlockEndSortOrder",
                table: "FlowChoiceOptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EndsFlow",
                table: "FlowChoiceOptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TargetStepSortOrder",
                table: "FlowChoiceOptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BlockEndSortOrder",
                table: "FlowBranches",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockEndSortOrder",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "CountDice",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "CountMode",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "BlockEndSortOrder",
                table: "FlowChoiceOptions");

            migrationBuilder.DropColumn(
                name: "EndsFlow",
                table: "FlowChoiceOptions");

            migrationBuilder.DropColumn(
                name: "TargetStepSortOrder",
                table: "FlowChoiceOptions");

            migrationBuilder.DropColumn(
                name: "BlockEndSortOrder",
                table: "FlowBranches");
        }
    }
}
