using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SkontoLoginHaertung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SkontoDebitorKontoNr",
                table: "FibuKonfiguration",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkontoKreditorKontoNr",
                table: "FibuKonfiguration",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FehlgeschlageneVersuche",
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
                name: "PasswortWechselErforderlich",
                table: "Benutzer",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkontoDebitorKontoNr",
                table: "FibuKonfiguration");

            migrationBuilder.DropColumn(
                name: "SkontoKreditorKontoNr",
                table: "FibuKonfiguration");

            migrationBuilder.DropColumn(
                name: "FehlgeschlageneVersuche",
                table: "Benutzer");

            migrationBuilder.DropColumn(
                name: "GesperrtBis",
                table: "Benutzer");

            migrationBuilder.DropColumn(
                name: "PasswortWechselErforderlich",
                table: "Benutzer");
        }
    }
}
