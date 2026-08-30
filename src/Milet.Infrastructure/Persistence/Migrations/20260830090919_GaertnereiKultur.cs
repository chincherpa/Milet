using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GaertnereiKultur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lagerbewegungen_ArtikelId_LagerortId",
                table: "Lagerbewegungen");

            migrationBuilder.DropIndex(
                name: "IX_ArtikelBestaende_ArtikelId_LagerortId",
                table: "ArtikelBestaende");

            migrationBuilder.AddColumn<decimal>(
                name: "BreiteMeter",
                table: "Lagerorte",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GaertnereiplanId",
                table: "Lagerorte",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoeheMeter",
                table: "Lagerorte",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IstFeld",
                table: "Lagerorte",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PosXMeter",
                table: "Lagerorte",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PosYMeter",
                table: "Lagerorte",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KulturstufeId",
                table: "Lagerbewegungen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SektionId",
                table: "Lagerbewegungen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KulturstufeId",
                table: "InventurPositionen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SektionId",
                table: "InventurPositionen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KulturstufeId",
                table: "BelegPositionen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SektionId",
                table: "BelegPositionen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KulturstufeId",
                table: "ArtikelBestaende",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SektionId",
                table: "ArtikelBestaende",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BotanischerName",
                table: "Artikel",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IstKulturpflanze",
                table: "Artikel",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Gaertnereiplaene",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BreiteMeter = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    HoeheMeter = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Aktiv = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gaertnereiplaene", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kulturstufen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    IstVerkaufsfaehig = table.Column<bool>(type: "bit", nullable: false),
                    FarbeHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Aktiv = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kulturstufen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sektionen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LagerortId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PosXMeter = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    PosYMeter = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    BreiteMeter = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    HoeheMeter = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Aktiv = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErstelltVonId = table.Column<int>(type: "int", nullable: true),
                    GeaendertAm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sektionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sektionen_Lagerorte_LagerortId",
                        column: x => x.LagerortId,
                        principalTable: "Lagerorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lagerorte_GaertnereiplanId",
                table: "Lagerorte",
                column: "GaertnereiplanId");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_ArtikelId_LagerortId_SektionId_KulturstufeId",
                table: "Lagerbewegungen",
                columns: new[] { "ArtikelId", "LagerortId", "SektionId", "KulturstufeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_KulturstufeId",
                table: "Lagerbewegungen",
                column: "KulturstufeId");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_SektionId",
                table: "Lagerbewegungen",
                column: "SektionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventurPositionen_KulturstufeId",
                table: "InventurPositionen",
                column: "KulturstufeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventurPositionen_SektionId",
                table: "InventurPositionen",
                column: "SektionId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_KulturstufeId",
                table: "BelegPositionen",
                column: "KulturstufeId");

            migrationBuilder.CreateIndex(
                name: "IX_BelegPositionen_SektionId",
                table: "BelegPositionen",
                column: "SektionId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelBestaende_ArtikelId_LagerortId_SektionId_KulturstufeId",
                table: "ArtikelBestaende",
                columns: new[] { "ArtikelId", "LagerortId", "SektionId", "KulturstufeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelBestaende_KulturstufeId",
                table: "ArtikelBestaende",
                column: "KulturstufeId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelBestaende_SektionId",
                table: "ArtikelBestaende",
                column: "SektionId");

            migrationBuilder.CreateIndex(
                name: "IX_Kulturstufen_Code",
                table: "Kulturstufen",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kulturstufen_Reihenfolge",
                table: "Kulturstufen",
                column: "Reihenfolge",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sektionen_LagerortId_Code",
                table: "Sektionen",
                columns: new[] { "LagerortId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtikelBestaende_Kulturstufen_KulturstufeId",
                table: "ArtikelBestaende",
                column: "KulturstufeId",
                principalTable: "Kulturstufen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtikelBestaende_Sektionen_SektionId",
                table: "ArtikelBestaende",
                column: "SektionId",
                principalTable: "Sektionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BelegPositionen_Kulturstufen_KulturstufeId",
                table: "BelegPositionen",
                column: "KulturstufeId",
                principalTable: "Kulturstufen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BelegPositionen_Sektionen_SektionId",
                table: "BelegPositionen",
                column: "SektionId",
                principalTable: "Sektionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventurPositionen_Kulturstufen_KulturstufeId",
                table: "InventurPositionen",
                column: "KulturstufeId",
                principalTable: "Kulturstufen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventurPositionen_Sektionen_SektionId",
                table: "InventurPositionen",
                column: "SektionId",
                principalTable: "Sektionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lagerbewegungen_Kulturstufen_KulturstufeId",
                table: "Lagerbewegungen",
                column: "KulturstufeId",
                principalTable: "Kulturstufen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lagerbewegungen_Sektionen_SektionId",
                table: "Lagerbewegungen",
                column: "SektionId",
                principalTable: "Sektionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lagerorte_Gaertnereiplaene_GaertnereiplanId",
                table: "Lagerorte",
                column: "GaertnereiplanId",
                principalTable: "Gaertnereiplaene",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArtikelBestaende_Kulturstufen_KulturstufeId",
                table: "ArtikelBestaende");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtikelBestaende_Sektionen_SektionId",
                table: "ArtikelBestaende");

            migrationBuilder.DropForeignKey(
                name: "FK_BelegPositionen_Kulturstufen_KulturstufeId",
                table: "BelegPositionen");

            migrationBuilder.DropForeignKey(
                name: "FK_BelegPositionen_Sektionen_SektionId",
                table: "BelegPositionen");

            migrationBuilder.DropForeignKey(
                name: "FK_InventurPositionen_Kulturstufen_KulturstufeId",
                table: "InventurPositionen");

            migrationBuilder.DropForeignKey(
                name: "FK_InventurPositionen_Sektionen_SektionId",
                table: "InventurPositionen");

            migrationBuilder.DropForeignKey(
                name: "FK_Lagerbewegungen_Kulturstufen_KulturstufeId",
                table: "Lagerbewegungen");

            migrationBuilder.DropForeignKey(
                name: "FK_Lagerbewegungen_Sektionen_SektionId",
                table: "Lagerbewegungen");

            migrationBuilder.DropForeignKey(
                name: "FK_Lagerorte_Gaertnereiplaene_GaertnereiplanId",
                table: "Lagerorte");

            migrationBuilder.DropTable(
                name: "Gaertnereiplaene");

            migrationBuilder.DropTable(
                name: "Kulturstufen");

            migrationBuilder.DropTable(
                name: "Sektionen");

            migrationBuilder.DropIndex(
                name: "IX_Lagerorte_GaertnereiplanId",
                table: "Lagerorte");

            migrationBuilder.DropIndex(
                name: "IX_Lagerbewegungen_ArtikelId_LagerortId_SektionId_KulturstufeId",
                table: "Lagerbewegungen");

            migrationBuilder.DropIndex(
                name: "IX_Lagerbewegungen_KulturstufeId",
                table: "Lagerbewegungen");

            migrationBuilder.DropIndex(
                name: "IX_Lagerbewegungen_SektionId",
                table: "Lagerbewegungen");

            migrationBuilder.DropIndex(
                name: "IX_InventurPositionen_KulturstufeId",
                table: "InventurPositionen");

            migrationBuilder.DropIndex(
                name: "IX_InventurPositionen_SektionId",
                table: "InventurPositionen");

            migrationBuilder.DropIndex(
                name: "IX_BelegPositionen_KulturstufeId",
                table: "BelegPositionen");

            migrationBuilder.DropIndex(
                name: "IX_BelegPositionen_SektionId",
                table: "BelegPositionen");

            migrationBuilder.DropIndex(
                name: "IX_ArtikelBestaende_ArtikelId_LagerortId_SektionId_KulturstufeId",
                table: "ArtikelBestaende");

            migrationBuilder.DropIndex(
                name: "IX_ArtikelBestaende_KulturstufeId",
                table: "ArtikelBestaende");

            migrationBuilder.DropIndex(
                name: "IX_ArtikelBestaende_SektionId",
                table: "ArtikelBestaende");

            migrationBuilder.DropColumn(
                name: "BreiteMeter",
                table: "Lagerorte");

            migrationBuilder.DropColumn(
                name: "GaertnereiplanId",
                table: "Lagerorte");

            migrationBuilder.DropColumn(
                name: "HoeheMeter",
                table: "Lagerorte");

            migrationBuilder.DropColumn(
                name: "IstFeld",
                table: "Lagerorte");

            migrationBuilder.DropColumn(
                name: "PosXMeter",
                table: "Lagerorte");

            migrationBuilder.DropColumn(
                name: "PosYMeter",
                table: "Lagerorte");

            migrationBuilder.DropColumn(
                name: "KulturstufeId",
                table: "Lagerbewegungen");

            migrationBuilder.DropColumn(
                name: "SektionId",
                table: "Lagerbewegungen");

            migrationBuilder.DropColumn(
                name: "KulturstufeId",
                table: "InventurPositionen");

            migrationBuilder.DropColumn(
                name: "SektionId",
                table: "InventurPositionen");

            migrationBuilder.DropColumn(
                name: "KulturstufeId",
                table: "BelegPositionen");

            migrationBuilder.DropColumn(
                name: "SektionId",
                table: "BelegPositionen");

            migrationBuilder.DropColumn(
                name: "KulturstufeId",
                table: "ArtikelBestaende");

            migrationBuilder.DropColumn(
                name: "SektionId",
                table: "ArtikelBestaende");

            migrationBuilder.DropColumn(
                name: "BotanischerName",
                table: "Artikel");

            migrationBuilder.DropColumn(
                name: "IstKulturpflanze",
                table: "Artikel");

            migrationBuilder.CreateIndex(
                name: "IX_Lagerbewegungen_ArtikelId_LagerortId",
                table: "Lagerbewegungen",
                columns: new[] { "ArtikelId", "LagerortId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtikelBestaende_ArtikelId_LagerortId",
                table: "ArtikelBestaende",
                columns: new[] { "ArtikelId", "LagerortId" },
                unique: true);
        }
    }
}
