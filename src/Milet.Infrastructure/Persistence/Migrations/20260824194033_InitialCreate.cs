using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Einheiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kuerzel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NachkommaStellen = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Einheiten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MwStSaetze",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bezeichnung = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Satz = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SteuerSchluessel = table.Column<int>(type: "int", nullable: true),
                    GueltigAb = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MwStSaetze", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nummernkreise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Jahr = table.Column<int>(type: "int", nullable: true),
                    NaechsteNummer = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nummernkreise", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Preislisten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GueltigVon = table.Column<DateOnly>(type: "date", nullable: true),
                    GueltigBis = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preislisten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Versandarten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kosten = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Versandarten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zahlungsbedingungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ZielTage = table.Column<int>(type: "int", nullable: false),
                    SkontoTage = table.Column<int>(type: "int", nullable: true),
                    SkontoProzent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zahlungsbedingungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Artikel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Artikelnummer = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EinheitId = table.Column<int>(type: "int", nullable: false),
                    MwStSatzId = table.Column<int>(type: "int", nullable: false),
                    Einkaufspreis = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Listenpreis = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Gewicht = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Ean = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IstLagerartikel = table.Column<bool>(type: "bit", nullable: false),
                    HatSeriennummern = table.Column<bool>(type: "bit", nullable: false),
                    Mindestbestand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Gesperrt = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artikel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artikel_Einheiten_EinheitId",
                        column: x => x.EinheitId,
                        principalTable: "Einheiten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Artikel_MwStSaetze_MwStSatzId",
                        column: x => x.MwStSatzId,
                        principalTable: "MwStSaetze",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kunden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kundennummer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Strasse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Plz = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Land = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Ansprechpartner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailRechnung = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UStIdNr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZahlungsbedingungId = table.Column<int>(type: "int", nullable: true),
                    PreislisteId = table.Column<int>(type: "int", nullable: true),
                    RabattProzent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Kreditlimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Liefersperre = table.Column<bool>(type: "bit", nullable: false),
                    DebitorenkontoNr = table.Column<int>(type: "int", nullable: true),
                    Notiz = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kunden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kunden_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kunden_Zahlungsbedingungen_ZahlungsbedingungId",
                        column: x => x.ZahlungsbedingungId,
                        principalTable: "Zahlungsbedingungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lieferanten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Lieferantennummer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Strasse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Plz = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Land = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Ansprechpartner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UStIdNr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZahlungsbedingungId = table.Column<int>(type: "int", nullable: true),
                    KreditorenkontoNr = table.Column<int>(type: "int", nullable: true),
                    Notiz = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lieferanten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lieferanten_Zahlungsbedingungen_ZahlungsbedingungId",
                        column: x => x.ZahlungsbedingungId,
                        principalTable: "Zahlungsbedingungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArtikelPreise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreislisteId = table.Column<int>(type: "int", nullable: false),
                    ArtikelId = table.Column<int>(type: "int", nullable: false),
                    AbMenge = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preis = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtikelPreise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtikelPreise_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtikelPreise_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artikel_Artikelnummer",
                table: "Artikel",
                column: "Artikelnummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artikel_EinheitId",
                table: "Artikel",
                column: "EinheitId");

            migrationBuilder.CreateIndex(
                name: "IX_Artikel_MwStSatzId",
                table: "Artikel",
                column: "MwStSatzId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelPreise_ArtikelId",
                table: "ArtikelPreise",
                column: "ArtikelId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelPreise_PreislisteId_ArtikelId_AbMenge",
                table: "ArtikelPreise",
                columns: new[] { "PreislisteId", "ArtikelId", "AbMenge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Einheiten_Kuerzel",
                table: "Einheiten",
                column: "Kuerzel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_Kundennummer",
                table: "Kunden",
                column: "Kundennummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_PreislisteId",
                table: "Kunden",
                column: "PreislisteId");

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_ZahlungsbedingungId",
                table: "Kunden",
                column: "ZahlungsbedingungId");

            migrationBuilder.CreateIndex(
                name: "IX_Lieferanten_Lieferantennummer",
                table: "Lieferanten",
                column: "Lieferantennummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lieferanten_ZahlungsbedingungId",
                table: "Lieferanten",
                column: "ZahlungsbedingungId");

            migrationBuilder.CreateIndex(
                name: "IX_Nummernkreise_Code_Jahr",
                table: "Nummernkreise",
                columns: new[] { "Code", "Jahr" },
                unique: true,
                filter: "[Jahr] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtikelPreise");

            migrationBuilder.DropTable(
                name: "Kunden");

            migrationBuilder.DropTable(
                name: "Lieferanten");

            migrationBuilder.DropTable(
                name: "Nummernkreise");

            migrationBuilder.DropTable(
                name: "Versandarten");

            migrationBuilder.DropTable(
                name: "Artikel");

            migrationBuilder.DropTable(
                name: "Preislisten");

            migrationBuilder.DropTable(
                name: "Zahlungsbedingungen");

            migrationBuilder.DropTable(
                name: "Einheiten");

            migrationBuilder.DropTable(
                name: "MwStSaetze");
        }
    }
}
