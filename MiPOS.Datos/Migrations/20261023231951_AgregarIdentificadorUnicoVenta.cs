using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiPOS.Datos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIdentificadorUnicoVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdentificadorUnico",
                table: "Ventas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_IdentificadorUnico",
                table: "Ventas",
                column: "IdentificadorUnico",
                unique: true,
                filter: "[IdentificadorUnico] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ventas_IdentificadorUnico",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "IdentificadorUnico",
                table: "Ventas");
        }
    }
}
