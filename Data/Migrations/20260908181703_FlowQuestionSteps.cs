using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlowQuestionSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OracleID",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // "Roll" rather than the generated "": every step that already exists rolls a fixed
            // oracle, because that was the only kind there was. An empty string is not a
            // FlowStepKind and would fail to parse the first time an old flow was opened.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "FlowSteps",
                type: "TEXT",
                nullable: false,
                defaultValue: "Roll");

            migrationBuilder.AddColumn<string>(
                name: "Question",
                table: "FlowSteps",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceStepSortOrder",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FlowChoiceOptions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowStepID = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OracleID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowChoiceOptions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FlowChoiceOptions_FlowSteps_FlowStepID",
                        column: x => x.FlowStepID,
                        principalTable: "FlowSteps",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlowChoiceOptions_Oracles_OracleID",
                        column: x => x.OracleID,
                        principalTable: "Oracles",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowChoiceOptions_FlowStepID",
                table: "FlowChoiceOptions",
                column: "FlowStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowChoiceOptions_OracleID",
                table: "FlowChoiceOptions",
                column: "OracleID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlowChoiceOptions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "Question",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "SourceStepSortOrder",
                table: "FlowSteps");

            migrationBuilder.AlterColumn<int>(
                name: "OracleID",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
