using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlowRepeatAndDropNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE FlowSteps SET Kind = 'Text' WHERE Kind = 'Number';");

            migrationBuilder.AddColumn<int>(
                name: "RepeatCount",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepeatCount",
                table: "FlowSteps");
        }
    }
}
