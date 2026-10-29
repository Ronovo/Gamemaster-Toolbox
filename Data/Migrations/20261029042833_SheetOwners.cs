using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GM_Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class SheetOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sheets from before owners have no owner to give them, so they are removed.
            migrationBuilder.Sql("DELETE FROM \"SheetVersions\";");
            migrationBuilder.Sql("DELETE FROM \"Sheets\";");

            migrationBuilder.DropIndex(
                name: "IX_Sheets_FileName",
                table: "Sheets");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Sheets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_OwnerId_FileName",
                table: "Sheets",
                columns: new[] { "OwnerId", "FileName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sheets_OwnerId_FileName",
                table: "Sheets");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Sheets");

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_FileName",
                table: "Sheets",
                column: "FileName");
        }
    }
}
