using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LagerLieferschein : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LagerortId",
                table: "BelegPositionen",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BelegTyp",
                table: "Belege",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.CreateTable(
                name: "Lagerorte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aktiv = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lagerorte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArtikelBestaende",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArtikelId = table.Column<int>(type: "int", nullable: false),
                    LagerortId = table.Column<int>(type: "int", nullable: false),
                    Menge = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtikelBestaende", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtikelBestaende_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtikelBestaende_Lagerorte_LagerortId",
                        column: x => x.LagerortId,
                        principalTable: "Lagerorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inventuren",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LagerortId = table.Column<int>(type: "int", nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventuren", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventuren_Lagerorte_LagerortId",
                        column: x => x.LagerortId,
                        principalTable: "Lagerorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seriennummern",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArtikelId = table.Column<int>(type: "int", nullable: false),
                    Nummer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LagerortId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seriennummern", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seriennummern_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seriennummern_Lagerorte_LagerortId",
                        column: x => x.LagerortId,
                        principalTable: "Lagerorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventurPositionen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventurId = table.Column<int>(type: "int", nullable: false),
                    ArtikelId = table.Column<int>(type: "int", nullable: false),
                    SollMenge = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IstMenge = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventurPositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventurPositionen_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventurPositionen_Inventuren_InventurId",
                        column: x => x.InventurId,
                        principalTable: "Inventuren",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BelegPositionSeriennummern",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelegPositionId = table.Column<int>(type: "int", nullable: false),
                    SeriennummerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BelegPositionSeriennummern", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BelegPositionSeriennummern_BelegPositionen_BelegPositionId",
                        column: x => x.BelegPositionId,
                        principalTable: "BelegPositionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BelegPositionSeriennummern_Seriennummern_SeriennummerId",
                        column: x => x.SeriennummerId,
                        principalTable: "Seriennummern",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lagerbewegungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArtikelId = table.Column<int>(type: "int", nullable: false),
                    LagerortId = table.Column<int>(type: "int", nullable: false),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Menge = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BelegPositionId = table.Column<int>(type: "int", nullable: true),
                    SeriennummerId = table.Column<int>(type: "int", nullable: true),
                    Zeitpunkt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BenutzerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lagerbewegungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lagerbewegungen_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lagerbewegungen_BelegPositionen_BelegPositionId",
                        column: x => x.BelegPositionId,
                        principalTable: "BelegPositionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lagerbewegungen_Lagerorte_LagerortId",
                        column: x => x.LagerortId,
                        principalTable: "Lagerorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lagerbewegungen_Seriennummern_SeriennummerId",
                        column: x => x.SeriennummerId,
                        principalTable: "Seriennummern",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_LagerortId",
                table: "BelegPositionen",
                column: "LagerortId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelBestaende_ArtikelId_LagerortId",
                table: "ArtikelBestaende",
                columns: new[] { "ArtikelId", "LagerortId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelBestaende_LagerortId",
                table: "ArtikelBestaende",
                column: "LagerortId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionSeriennummern_BelegPositionId_SeriennummerId",
                table: "BelegPositionSeriennummern",
                columns: new[] { "BelegPositionId", "SeriennummerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionSeriennummern_SeriennummerId",
                table: "BelegPositionSeriennummern",
                column: "SeriennummerId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventuren_LagerortId",
                table: "Inventuren",
                column: "LagerortId");

            migrationBuilder.CreateIndex(
                name: "IX_InventurPositionen_ArtikelId",
                table: "InventurPositionen",
                column: "ArtikelId");

            migrationBuilder.CreateIndex(
                name: "IX_InventurPositionen_InventurId",
                table: "InventurPositionen",
                column: "InventurId");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_ArtikelId_LagerortId",
                table: "Lagerbewegungen",
                columns: new[] { "ArtikelId", "LagerortId" });

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_BelegPositionId",
                table: "Lagerbewegungen",
                column: "BelegPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_LagerortId",
                table: "Lagerbewegungen",
                column: "LagerortId");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_SeriennummerId",
                table: "Lagerbewegungen",
                column: "SeriennummerId");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerorte_Code",
                table: "Lagerorte",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seriennummern_ArtikelId_Nummer",
                table: "Seriennummern",
                columns: new[] { "ArtikelId", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seriennummern_LagerortId",
                table: "Seriennummern",
                column: "LagerortId");

            migrationBuilder.AddForeignKey(
                name: "FK_BelegPositionen_Lagerorte_LagerortId",
                table: "BelegPositionen",
                column: "LagerortId",
                principalTable: "Lagerorte",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BelegPositionen_Lagerorte_LagerortId",
                table: "BelegPositionen");

            migrationBuilder.DropTable(
                name: "ArtikelBestaende");

            migrationBuilder.DropTable(
                name: "BelegPositionSeriennummern");

            migrationBuilder.DropTable(
                name: "InventurPositionen");

            migrationBuilder.DropTable(
                name: "Lagerbewegungen");

            migrationBuilder.DropTable(
                name: "Inventuren");

            migrationBuilder.DropTable(
                name: "Seriennummern");

            migrationBuilder.DropTable(
                name: "Lagerorte");

            migrationBuilder.DropIndex(
                name: "IX_BelegPositionen_LagerortId",
                table: "BelegPositionen");

            migrationBuilder.DropColumn(
                name: "LagerortId",
                table: "BelegPositionen");

            migrationBuilder.AlterColumn<string>(
                name: "BelegTyp",
                table: "Belege",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(13)",
                oldMaxLength: 13);
        }
    }
}
