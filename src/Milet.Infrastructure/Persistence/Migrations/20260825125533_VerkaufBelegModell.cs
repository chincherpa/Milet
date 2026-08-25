using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerkaufBelegModell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Belege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelegNummer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BelegDatum = table.Column<DateOnly>(type: "date", nullable: false),
                    KundeId = table.Column<int>(type: "int", nullable: false),
                    RgAdr_Name1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RgAdr_Name2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RgAdr_Strasse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RgAdr_Plz = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RgAdr_Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RgAdr_Land = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LfAdr_Name1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LfAdr_Name2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LfAdr_Strasse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LfAdr_Plz = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LfAdr_Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LfAdr_Land = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ZahlungsbedingungZielTage = table.Column<int>(type: "int", nullable: false),
                    ZahlungsbedingungSkontoTage = table.Column<int>(type: "int", nullable: true),
                    ZahlungsbedingungSkontoProzent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SummeNetto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SummeMwSt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SummeBrutto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Faelligkeit = table.Column<DateOnly>(type: "date", nullable: true),
                    Leistungsdatum = table.Column<DateOnly>(type: "date", nullable: true),
                    Kopftext = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Fusstext = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    BelegTyp = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Belege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Belege_Kunden_KundeId",
                        column: x => x.KundeId,
                        principalTable: "Kunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Firmenstamm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Firmenname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Strasse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Plz = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Land = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UStIdNr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bic = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firmenstamm", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BelegPositionen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelegId = table.Column<int>(type: "int", nullable: false),
                    PositionsNr = table.Column<int>(type: "int", nullable: false),
                    PositionsTyp = table.Column<int>(type: "int", nullable: false),
                    ArtikelId = table.Column<int>(type: "int", nullable: true),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EinheitKuerzel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Menge = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Einzelpreis = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RabattProzent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MwStSatzId = table.Column<int>(type: "int", nullable: true),
                    MwStSatzWert = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SteuerSchluessel = table.Column<int>(type: "int", nullable: true),
                    GesamtNetto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UrsprungsPositionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BelegPositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BelegPositionen_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BelegPositionen_BelegPositionen_UrsprungsPositionId",
                        column: x => x.UrsprungsPositionId,
                        principalTable: "BelegPositionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BelegPositionen_Belege_BelegId",
                        column: x => x.BelegId,
                        principalTable: "Belege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BelegPositionen_MwStSaetze_MwStSatzId",
                        column: x => x.MwStSatzId,
                        principalTable: "MwStSaetze",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BelegSteuerSummen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelegId = table.Column<int>(type: "int", nullable: false),
                    MwStSatzWert = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SteuerSchluessel = table.Column<int>(type: "int", nullable: true),
                    NettoSumme = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MwStBetrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BelegSteuerSummen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BelegSteuerSummen_Belege_BelegId",
                        column: x => x.BelegId,
                        principalTable: "Belege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OffenePosten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelegId = table.Column<int>(type: "int", nullable: false),
                    KundeId = table.Column<int>(type: "int", nullable: false),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Betrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OffenerBetrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Faelligkeit = table.Column<DateOnly>(type: "date", nullable: false),
                    Mahnstufe = table.Column<int>(type: "int", nullable: false),
                    Mahnsperre = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OffenePosten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OffenePosten_Belege_BelegId",
                        column: x => x.BelegId,
                        principalTable: "Belege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Belege_BelegTyp_BelegNummer",
                table: "Belege",
                columns: new[] { "BelegTyp", "BelegNummer" },
                unique: true,
                filter: "[BelegNummer] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Belege_KundeId",
                table: "Belege",
                column: "KundeId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_ArtikelId",
                table: "BelegPositionen",
                column: "ArtikelId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_BelegId",
                table: "BelegPositionen",
                column: "BelegId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_MwStSatzId",
                table: "BelegPositionen",
                column: "MwStSatzId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_UrsprungsPositionId",
                table: "BelegPositionen",
                column: "UrsprungsPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegSteuerSummen_BelegId",
                table: "BelegSteuerSummen",
                column: "BelegId");

            migrationBuilder.CreateIndex(
                name: "IX_OffenePosten_BelegId",
                table: "OffenePosten",
                column: "BelegId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BelegPositionen");

            migrationBuilder.DropTable(
                name: "BelegSteuerSummen");

            migrationBuilder.DropTable(
                name: "Firmenstamm");

            migrationBuilder.DropTable(
                name: "OffenePosten");

            migrationBuilder.DropTable(
                name: "Belege");
        }
    }
}
