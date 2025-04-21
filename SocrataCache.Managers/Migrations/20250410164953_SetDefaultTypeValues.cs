using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocrataCache.DatasetManager.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultTypeValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Dataset SET Type = 'csv' WHERE Type = null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
