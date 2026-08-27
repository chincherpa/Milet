using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanzenMahnwesen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "OffenePosten",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Bestehende Zeilen anhand OffenerBetrag/Betrag herleiten statt pauschal auf "Offen" (0) zu belassen —
            // 0 = Offen, 1 = TeilweiseBezahlt, 2 = Ausgeglichen (siehe OffenerPostenStatus).
            migrationBuilder.Sql(@"
UPDATE OffenePosten
SET Status = CASE
    WHEN OffenerBetrag <= 0 THEN 2
    WHEN OffenerBetrag < Betrag THEN 1
    ELSE 0
END;");

            migrationBuilder.CreateTable(
                name: "Mahnstufen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Stufe = table.Column<int>(type: "int", nullable: false),
                    Karenztage = table.Column<int>(type: "int", nullable: false),
                    Gebuehr = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mahntext = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mahnstufen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mahnungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundeId = table.Column<int>(type: "int", nullable: false),
                    MahnDatum = table.Column<DateOnly>(type: "date", nullable: false),
                    Mahnstufe = table.Column<int>(type: "int", nullable: false),
                    Gebuehr = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Gesamtbetrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mahnungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mahnungen_Kunden_KundeId",
                        column: x => x.KundeId,
                        principalTable: "Kunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zahlungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundeId = table.Column<int>(type: "int", nullable: true),
                    LieferantId = table.Column<int>(type: "int", nullable: true),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Zahlungsdatum = table.Column<DateOnly>(type: "date", nullable: false),
                    Gesamtbetrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Zahlungsart = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Referenz = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zahlungen", x => x.Id);
                    table.CheckConstraint("CK_Zahlungen_KundeOderLieferant", "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Zahlungen_Kunden_KundeId",
                        column: x => x.KundeId,
                        principalTable: "Kunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Zahlungen_Lieferanten_LieferantId",
                        column: x => x.LieferantId,
                        principalTable: "Lieferanten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailVersand",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelegId = table.Column<int>(type: "int", nullable: true),
                    MahnungId = table.Column<int>(type: "int", nullable: true),
                    Empfaenger = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Betreff = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GesendetAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Erfolgreich = table.Column<bool>(type: "bit", nullable: false),
                    Fehlermeldung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVersand", x => x.Id);
                    table.CheckConstraint("CK_EmailVersand_BelegOderMahnung", "([BelegId] IS NOT NULL AND [MahnungId] IS NULL) OR ([BelegId] IS NULL AND [MahnungId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_EmailVersand_Belege_BelegId",
                        column: x => x.BelegId,
                        principalTable: "Belege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailVersand_Mahnungen_MahnungId",
                        column: x => x.MahnungId,
                        principalTable: "Mahnungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MahnungPositionen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MahnungId = table.Column<int>(type: "int", nullable: false),
                    OffenerPostenId = table.Column<int>(type: "int", nullable: false),
                    BelegNummerSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OffenerBetragSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MahnungPositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MahnungPositionen_Mahnungen_MahnungId",
                        column: x => x.MahnungId,
                        principalTable: "Mahnungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MahnungPositionen_OffenePosten_OffenerPostenId",
                        column: x => x.OffenerPostenId,
                        principalTable: "OffenePosten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZahlungZuordnungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZahlungId = table.Column<int>(type: "int", nullable: false),
                    OffenerPostenId = table.Column<int>(type: "int", nullable: false),
                    Betrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SkontoBetrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZahlungZuordnungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZahlungZuordnungen_OffenePosten_OffenerPostenId",
                        column: x => x.OffenerPostenId,
                        principalTable: "OffenePosten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZahlungZuordnungen_Zahlungen_ZahlungId",
                        column: x => x.ZahlungId,
                        principalTable: "Zahlungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVersand_BelegId",
                table: "EmailVersand",
                column: "BelegId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVersand_MahnungId",
                table: "EmailVersand",
                column: "MahnungId");

            migrationBuilder.CreateIndex(
                name: "IX_Mahnstufen_Stufe",
                table: "Mahnstufen",
                column: "Stufe",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mahnungen_KundeId",
                table: "Mahnungen",
                column: "KundeId");

            migrationBuilder.CreateIndex(
                name: "IX_MahnungPositionen_MahnungId",
                table: "MahnungPositionen",
                column: "MahnungId");

            migrationBuilder.CreateIndex(
                name: "IX_MahnungPositionen_OffenerPostenId",
                table: "MahnungPositionen",
                column: "OffenerPostenId");

            migrationBuilder.CreateIndex(
                name: "IX_Zahlungen_KundeId",
                table: "Zahlungen",
                column: "KundeId");

            migrationBuilder.CreateIndex(
                name: "IX_Zahlungen_LieferantId",
                table: "Zahlungen",
                column: "LieferantId");

            migrationBuilder.CreateIndex(
                name: "IX_ZahlungZuordnungen_OffenerPostenId",
                table: "ZahlungZuordnungen",
                column: "OffenerPostenId");

            migrationBuilder.CreateIndex(
                name: "IX_ZahlungZuordnungen_ZahlungId",
                table: "ZahlungZuordnungen",
                column: "ZahlungId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVersand");

            migrationBuilder.DropTable(
                name: "Mahnstufen");

            migrationBuilder.DropTable(
                name: "MahnungPositionen");

            migrationBuilder.DropTable(
                name: "ZahlungZuordnungen");

            migrationBuilder.DropTable(
                name: "Mahnungen");

            migrationBuilder.DropTable(
                name: "Zahlungen");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OffenePosten");
        }
    }
}
