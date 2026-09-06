using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Befund 13 (Fehlversuchszähler + Zeitsperre am Login), 30 (erzwungener Wechsel des Initialpassworts)
    /// und 7 (Skontokonten in der FibuKonfiguration statt fest im DatevExportService) aus
    /// REVIEW_2026-08-29.md.
    ///
    /// Reine Spaltenergänzungen mit Defaults — auf einer bestehenden Datenbank problemlos anwendbar.
    /// Zwei Dinge trägt die Migration bewusst NICHT nach, weil sie fachlich in den Seed gehören und dort
    /// idempotent laufen: <c>PasswortAenderungErforderlich</c> für einen Alt-Admin, der noch das
    /// dokumentierte Initialpasswort hat (AdminSeed), und die Standard-Skontokonten (StammdatenSeed legt
    /// die FibuKonfiguration nur an, wenn sie fehlt — eine bestehende bleibt bei 0 und fällt damit auf die
    /// Kontenrahmen-Standardwerte im DatevExportService zurück, wie bisher).
    /// </summary>
    public partial class SicherheitUndSkontokonten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SkontoKontoDebitorNr",
                table: "FibuKonfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkontoKontoKreditorNr",
                table: "FibuKonfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FehlversuchZaehler",
                table: "Benutzer",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GesperrtBis",
                table: "Benutzer",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PasswortAenderungErforderlich",
                table: "Benutzer",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkontoKontoDebitorNr",
                table: "FibuKonfiguration");

            migrationBuilder.DropColumn(
                name: "SkontoKontoKreditorNr",
                table: "FibuKonfiguration");

            migrationBuilder.DropColumn(
                name: "FehlversuchZaehler",
                table: "Benutzer");

            migrationBuilder.DropColumn(
                name: "GesperrtBis",
                table: "Benutzer");

            migrationBuilder.DropColumn(
                name: "PasswortAenderungErforderlich",
                table: "Benutzer");
        }
    }
}
