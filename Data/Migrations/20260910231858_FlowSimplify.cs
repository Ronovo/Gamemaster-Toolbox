using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlowSimplify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert data that used the retired features before their columns go away.

            // A Fields step becomes a Text question whose text is its labels.
            migrationBuilder.Sql(@"
                UPDATE FlowSteps
                SET Question = COALESCE(
                        NULLIF((SELECT group_concat(Label, ', ')
                                FROM FlowFields
                                WHERE FlowFields.FlowStepID = FlowSteps.ID), ''),
                        NULLIF(Question, ''),
                        'Fill in the details')
                WHERE Kind = 'Fields';");
            migrationBuilder.Sql("UPDATE FlowSteps SET Kind = 'Text' WHERE Kind = 'Fields';");

            // A loop is gone: detach its body (so the cascade does not take it) then drop it.
            migrationBuilder.Sql("UPDATE FlowSteps SET ParentStepID = NULL WHERE ParentStepID IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM FlowSteps WHERE Kind = 'Loop';");

            // Renumber each flow's steps to a clean 1..N in their existing order.
            migrationBuilder.Sql(@"
                WITH ordered AS (
                    SELECT ID, ROW_NUMBER() OVER (PARTITION BY FlowID ORDER BY SortOrder, ID) AS rn
                    FROM FlowSteps
                )
                UPDATE FlowSteps
                SET SortOrder = (SELECT rn FROM ordered WHERE ordered.ID = FlowSteps.ID);");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowSteps_FlowSteps_ParentStepID",
                table: "FlowSteps");

            migrationBuilder.DropTable(
                name: "FlowFields");

            migrationBuilder.DropIndex(
                name: "IX_FlowSteps_ParentStepID",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "CountDice",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "CountMode",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "ParentStepID",
                table: "FlowSteps");

            migrationBuilder.DropColumn(
                name: "RepeatCount",
                table: "FlowSteps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ParentStepID",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatCount",
                table: "FlowSteps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FlowFields",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowStepID = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowFields", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FlowFields_FlowSteps_FlowStepID",
                        column: x => x.FlowStepID,
                        principalTable: "FlowSteps",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowSteps_ParentStepID",
                table: "FlowSteps",
                column: "ParentStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowFields_FlowStepID",
                table: "FlowFields",
                column: "FlowStepID");

            migrationBuilder.AddForeignKey(
                name: "FK_FlowSteps_FlowSteps_ParentStepID",
                table: "FlowSteps",
                column: "ParentStepID",
                principalTable: "FlowSteps",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
