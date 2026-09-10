using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlowContainmentAndFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockEndSortOrder",
                table: "FlowChoiceOptions");

            migrationBuilder.RenameColumn(
                name: "SourceStepSortOrder",
                table: "FlowSteps",
                newName: "SourceStepID");

            migrationBuilder.RenameColumn(
                name: "BlockEndSortOrder",
                table: "FlowSteps",
                newName: "ParentStepID");

            migrationBuilder.RenameColumn(
                name: "TargetStepSortOrder",
                table: "FlowChoiceOptions",
                newName: "TargetStepID");

            migrationBuilder.RenameColumn(
                name: "TargetStepSortOrder",
                table: "FlowBranches",
                newName: "TargetStepID");

            migrationBuilder.RenameColumn(
                name: "BlockEndSortOrder",
                table: "FlowBranches",
                newName: "OracleID");

            migrationBuilder.AddColumn<bool>(
                name: "EndsFlow",
                table: "FlowBranches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FlowFields",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowStepID = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
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
                name: "IX_FlowSteps_SourceStepID",
                table: "FlowSteps",
                column: "SourceStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowChoiceOptions_TargetStepID",
                table: "FlowChoiceOptions",
                column: "TargetStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowBranches_OracleID",
                table: "FlowBranches",
                column: "OracleID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowBranches_TargetStepID",
                table: "FlowBranches",
                column: "TargetStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowFields_FlowStepID",
                table: "FlowFields",
                column: "FlowStepID");

            migrationBuilder.AddForeignKey(
                name: "FK_FlowBranches_FlowSteps_TargetStepID",
                table: "FlowBranches",
                column: "TargetStepID",
                principalTable: "FlowSteps",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_FlowBranches_Oracles_OracleID",
                table: "FlowBranches",
                column: "OracleID",
                principalTable: "Oracles",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowChoiceOptions_FlowSteps_TargetStepID",
                table: "FlowChoiceOptions",
                column: "TargetStepID",
                principalTable: "FlowSteps",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_FlowSteps_FlowSteps_ParentStepID",
                table: "FlowSteps",
                column: "ParentStepID",
                principalTable: "FlowSteps",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FlowSteps_FlowSteps_SourceStepID",
                table: "FlowSteps",
                column: "SourceStepID",
                principalTable: "FlowSteps",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FlowBranches_FlowSteps_TargetStepID",
                table: "FlowBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowBranches_Oracles_OracleID",
                table: "FlowBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowChoiceOptions_FlowSteps_TargetStepID",
                table: "FlowChoiceOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowSteps_FlowSteps_ParentStepID",
                table: "FlowSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_FlowSteps_FlowSteps_SourceStepID",
                table: "FlowSteps");

            migrationBuilder.DropTable(
                name: "FlowFields");

            migrationBuilder.DropIndex(
                name: "IX_FlowSteps_ParentStepID",
                table: "FlowSteps");

            migrationBuilder.DropIndex(
                name: "IX_FlowSteps_SourceStepID",
                table: "FlowSteps");

            migrationBuilder.DropIndex(
                name: "IX_FlowChoiceOptions_TargetStepID",
                table: "FlowChoiceOptions");

            migrationBuilder.DropIndex(
                name: "IX_FlowBranches_OracleID",
                table: "FlowBranches");

            migrationBuilder.DropIndex(
                name: "IX_FlowBranches_TargetStepID",
                table: "FlowBranches");

            migrationBuilder.DropColumn(
                name: "EndsFlow",
                table: "FlowBranches");

            migrationBuilder.RenameColumn(
                name: "SourceStepID",
                table: "FlowSteps",
                newName: "SourceStepSortOrder");

            migrationBuilder.RenameColumn(
                name: "ParentStepID",
                table: "FlowSteps",
                newName: "BlockEndSortOrder");

            migrationBuilder.RenameColumn(
                name: "TargetStepID",
                table: "FlowChoiceOptions",
                newName: "TargetStepSortOrder");

            migrationBuilder.RenameColumn(
                name: "TargetStepID",
                table: "FlowBranches",
                newName: "TargetStepSortOrder");

            migrationBuilder.RenameColumn(
                name: "OracleID",
                table: "FlowBranches",
                newName: "BlockEndSortOrder");

            migrationBuilder.AddColumn<int>(
                name: "BlockEndSortOrder",
                table: "FlowChoiceOptions",
                type: "INTEGER",
                nullable: true);
        }
    }
}
