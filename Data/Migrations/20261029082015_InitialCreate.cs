using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameSystems",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Genre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSystems", x => x.ID);
                    table.ForeignKey(
                        name: "FK_GameSystems_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flows",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    GameSystemID = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flows", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Flows_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Flows_GameSystems_GameSystemID",
                        column: x => x.GameSystemID,
                        principalTable: "GameSystems",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Oracles",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GameSystemID = table.Column<int>(type: "INTEGER", nullable: false),
                    OracleSubtype = table.Column<string>(type: "TEXT", nullable: false),
                    DiceCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DiceFaces = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oracles", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Oracles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Oracles_GameSystems_GameSystemID",
                        column: x => x.GameSystemID,
                        principalTable: "GameSystems",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Sheets",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    GameSystemID = table.Column<int>(type: "INTEGER", nullable: true),
                    DateUploaded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sheets", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Sheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sheets_GameSystems_GameSystemID",
                        column: x => x.GameSystemID,
                        principalTable: "GameSystems",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "FlowSteps",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowID = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    OracleID = table.Column<int>(type: "INTEGER", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Question = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SourceStepID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowSteps", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FlowSteps_FlowSteps_SourceStepID",
                        column: x => x.SourceStepID,
                        principalTable: "FlowSteps",
                        principalColumn: "ID");
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
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "OracleEntries",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OracleID = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RangeStart = table.Column<int>(type: "INTEGER", nullable: true),
                    RangeEnd = table.Column<int>(type: "INTEGER", nullable: true),
                    CardCode = table.Column<string>(type: "TEXT", nullable: true),
                    CardLabel = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OracleEntries", x => x.ID);
                    table.ForeignKey(
                        name: "FK_OracleEntries_Oracles_OracleID",
                        column: x => x.OracleID,
                        principalTable: "Oracles",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SheetVersions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SheetID = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetVersions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SheetVersions_Sheets_SheetID",
                        column: x => x.SheetID,
                        principalTable: "Sheets",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowChoiceOptions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowStepID = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OracleID = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetStepID = table.Column<int>(type: "INTEGER", nullable: true),
                    EndsFlow = table.Column<bool>(type: "INTEGER", nullable: false)
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
                        name: "FK_FlowChoiceOptions_FlowSteps_TargetStepID",
                        column: x => x.TargetStepID,
                        principalTable: "FlowSteps",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_FlowChoiceOptions_Oracles_OracleID",
                        column: x => x.OracleID,
                        principalTable: "Oracles",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "FlowBranches",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowStepID = table.Column<int>(type: "INTEGER", nullable: false),
                    OracleEntryID = table.Column<int>(type: "INTEGER", nullable: false),
                    OracleID = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetStepID = table.Column<int>(type: "INTEGER", nullable: true),
                    EndsFlow = table.Column<bool>(type: "INTEGER", nullable: false)
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
                        name: "FK_FlowBranches_FlowSteps_TargetStepID",
                        column: x => x.TargetStepID,
                        principalTable: "FlowSteps",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_FlowBranches_OracleEntries_OracleEntryID",
                        column: x => x.OracleEntryID,
                        principalTable: "OracleEntries",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlowBranches_Oracles_OracleID",
                        column: x => x.OracleID,
                        principalTable: "Oracles",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

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
                name: "IX_FlowBranches_OracleID",
                table: "FlowBranches",
                column: "OracleID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowBranches_TargetStepID",
                table: "FlowBranches",
                column: "TargetStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowChoiceOptions_FlowStepID",
                table: "FlowChoiceOptions",
                column: "FlowStepID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowChoiceOptions_OracleID",
                table: "FlowChoiceOptions",
                column: "OracleID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowChoiceOptions_TargetStepID",
                table: "FlowChoiceOptions",
                column: "TargetStepID");

            migrationBuilder.CreateIndex(
                name: "IX_Flows_GameSystemID",
                table: "Flows",
                column: "GameSystemID");

            migrationBuilder.CreateIndex(
                name: "IX_Flows_OwnerId",
                table: "Flows",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowSteps_FlowID",
                table: "FlowSteps",
                column: "FlowID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowSteps_OracleID",
                table: "FlowSteps",
                column: "OracleID");

            migrationBuilder.CreateIndex(
                name: "IX_FlowSteps_SourceStepID",
                table: "FlowSteps",
                column: "SourceStepID");

            migrationBuilder.CreateIndex(
                name: "IX_GameSystems_OwnerId_Name",
                table: "GameSystems",
                columns: new[] { "OwnerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OracleEntries_OracleID",
                table: "OracleEntries",
                column: "OracleID");

            migrationBuilder.CreateIndex(
                name: "IX_Oracles_GameSystemID",
                table: "Oracles",
                column: "GameSystemID");

            migrationBuilder.CreateIndex(
                name: "IX_Oracles_OwnerId",
                table: "Oracles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_GameSystemID",
                table: "Sheets",
                column: "GameSystemID");

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_OwnerId_FileName",
                table: "Sheets",
                columns: new[] { "OwnerId", "FileName" });

            migrationBuilder.CreateIndex(
                name: "IX_SheetVersions_SheetID_Version",
                table: "SheetVersions",
                columns: new[] { "SheetID", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "FlowBranches");

            migrationBuilder.DropTable(
                name: "FlowChoiceOptions");

            migrationBuilder.DropTable(
                name: "SheetVersions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "OracleEntries");

            migrationBuilder.DropTable(
                name: "FlowSteps");

            migrationBuilder.DropTable(
                name: "Sheets");

            migrationBuilder.DropTable(
                name: "Flows");

            migrationBuilder.DropTable(
                name: "Oracles");

            migrationBuilder.DropTable(
                name: "GameSystems");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
