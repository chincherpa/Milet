using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Befunde 2/3 (Lagerbewegungen.Bemerkung — Freitext der Kulturbuchungen und der Grund einer
    /// Bestandskorrektur wurden bis dahin verworfen), 15 (InventurPositionen.RowVersion — parallele Zählungen
    /// überschrieben sich stillschweigend) und 7 (gefilterter Unique-Index: höchstens eine OFFENE Inventur je
    /// Lagerort; der Guard in InventurService war ein Read-then-Insert ohne Sperre).
    ///
    /// ACHTUNG bei einer bestehenden Datenbank: gibt es dort bereits zwei offene Inventuren desselben
    /// Lagerorts, scheitert das Anlegen des Index. Diese Inventuren müssen vorher abgeschlossen oder gelöscht
    /// werden — genau der Zustand, den der Index künftig verhindert. Abfrage zum Prüfen:
    /// SELECT LagerortId, COUNT(*) FROM Inventuren WHERE Status = 0 GROUP BY LagerortId HAVING COUNT(*) &gt; 1
    /// </summary>
    public partial class ReviewFixes20260906 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventuren_LagerortId",
                table: "Inventuren");

            migrationBuilder.AddColumn<string>(
                name: "Bemerkung",
                table: "Lagerbewegungen",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventurPositionen",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Inventuren_LagerortId",
                table: "Inventuren",
                column: "LagerortId",
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventuren_LagerortId",
                table: "Inventuren");

            migrationBuilder.DropColumn(
                name: "Bemerkung",
                table: "Lagerbewegungen");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventurPositionen");

            migrationBuilder.CreateIndex(
                name: "IX_Inventuren_LagerortId",
                table: "Inventuren",
                column: "LagerortId");
        }
    }
}
