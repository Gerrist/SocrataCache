using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocrataCache.DatasetManager.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeColumnToDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Dataset",
                type: "TEXT",
                nullable: false,
                defaultValue: "csv");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Dataset");
        }
    }
}
