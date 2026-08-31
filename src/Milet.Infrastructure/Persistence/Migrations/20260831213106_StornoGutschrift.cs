using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StornoGutschrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorniertenBelegId",
                table: "Belege",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Belege_StorniertenBelegId",
                table: "Belege",
                column: "StorniertenBelegId");

            migrationBuilder.AddForeignKey(
                name: "FK_Belege_Belege_StorniertenBelegId",
                table: "Belege",
                column: "StorniertenBelegId",
                principalTable: "Belege",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Belege_Belege_StorniertenBelegId",
                table: "Belege");

            migrationBuilder.DropIndex(
                name: "IX_Belege_StorniertenBelegId",
                table: "Belege");

            migrationBuilder.DropColumn(
                name: "StorniertenBelegId",
                table: "Belege");
        }
    }
}
