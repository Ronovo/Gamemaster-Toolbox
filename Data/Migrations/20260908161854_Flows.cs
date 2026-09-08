using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class Flows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Flows",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    GameSystemID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flows", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Flows_GameSystems_GameSystemID",
                        column: x => x.GameSystemID,
                        principalTable: "GameSystems",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowSteps",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowID = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    OracleID = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowSteps", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FlowSteps_Flows_FlowID",
                        column: x => x.FlowID,
                        principalTable: "Flows",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlowSteps_Oracles_OracleID",
                        column: x => x.OracleID,
                        principalTable: "Oracles",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowBranches",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowStepID = table.Column<int>(type: "INTEGER", nullable: false),
                    OracleEntryID = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetStepSortOrder = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowBranches", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FlowBranches_FlowSteps_FlowStepID",
                        column: x => x.FlowStepID,
                        principalTable: "FlowSteps",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlowBranches_OracleEntries_OracleEntryID",
                        column: x => x.OracleEntryID,
                        principalTable: "OracleEntries",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowBranches_FlowStepID_OracleEntryID",
                table: "FlowBranches",
                columns: new[] { "FlowStepID", "OracleEntryID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowBranches_OracleEntryID",
                table: "FlowBranches",
                column: "OracleEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_Flows_GameSystemID",
                table: "Flows",
                column: "GameSystemID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowSteps_FlowID",
                table: "FlowSteps",
                column: "FlowID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowSteps_OracleID",
                table: "FlowSteps",
                column: "OracleID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlowBranches");

            migrationBuilder.DropTable(
                name: "FlowSteps");

            migrationBuilder.DropTable(
                name: "Flows");
        }
    }
}
