using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EinkaufBestellungWareneingang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "KundeId",
                table: "OffenePosten",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "LieferantId",
                table: "OffenePosten",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KundeId",
                table: "Belege",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "BelegTyp",
                table: "Belege",
                type: "nvarchar(21)",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(13)",
                oldMaxLength: 13);

            migrationBuilder.AddColumn<string>(
                name: "ExterneReferenz",
                table: "Belege",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LieferantId",
                table: "Belege",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OffenePosten_KundeOderLieferant",
                table: "OffenePosten",
                sql: "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Belege_LieferantId",
                table: "Belege",
                column: "LieferantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belege_KundeOderLieferant",
                table: "Belege",
                sql: "([KundeId] IS NOT NULL AND [LieferantId] IS NULL) OR ([KundeId] IS NULL AND [LieferantId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Belege_Lieferanten_LieferantId",
                table: "Belege",
                column: "LieferantId",
                principalTable: "Lieferanten",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Belege_Lieferanten_LieferantId",
                table: "Belege");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OffenePosten_KundeOderLieferant",
                table: "OffenePosten");

            migrationBuilder.DropIndex(
                name: "IX_Belege_LieferantId",
                table: "Belege");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Belege_KundeOderLieferant",
                table: "Belege");

            migrationBuilder.DropColumn(
                name: "LieferantId",
                table: "OffenePosten");

            migrationBuilder.DropColumn(
                name: "ExterneReferenz",
                table: "Belege");

            migrationBuilder.DropColumn(
                name: "LieferantId",
                table: "Belege");

            migrationBuilder.AlterColumn<int>(
                name: "KundeId",
                table: "OffenePosten",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KundeId",
                table: "Belege",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BelegTyp",
                table: "Belege",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(21)",
                oldMaxLength: 21);
        }
    }
}
