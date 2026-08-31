using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LagerbewegungBemerkung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bemerkung",
                table: "Lagerbewegungen",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bemerkung",
                table: "Lagerbewegungen");
        }
    }
}
