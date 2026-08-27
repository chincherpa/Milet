using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DatevReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExportiertAm",
                table: "Zahlungen",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AufwandskontoNr",
                table: "MwStSaetze",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ErloeskontoNr",
                table: "MwStSaetze",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExportiertAm",
                table: "Belege",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FibuKonfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Kontenrahmen = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BeraterNr = table.Column<int>(type: "int", nullable: false),
                    MandantNr = table.Column<int>(type: "int", nullable: false),
                    WirtschaftsjahrBeginnMonat = table.Column<int>(type: "int", nullable: false),
                    SachkontenLaenge = table.Column<int>(type: "int", nullable: false),
                    BankkontoNr = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FibuKonfiguration", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FibuKonfiguration");

            migrationBuilder.DropColumn(
                name: "ExportiertAm",
                table: "Zahlungen");

            migrationBuilder.DropColumn(
                name: "AufwandskontoNr",
                table: "MwStSaetze");

            migrationBuilder.DropColumn(
                name: "ErloeskontoNr",
                table: "MwStSaetze");

            migrationBuilder.DropColumn(
                name: "ExportiertAm",
                table: "Belege");
        }
    }
}
