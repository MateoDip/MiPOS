using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiPOS.Datos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFacturacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlicuotaIva",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 5); // los registros existentes quedan con IVA 21% (código 5 de ARCA)

            migrationBuilder.AddColumn<int>(
                name: "AlicuotaIva",
                table: "DetallesVenta",
                type: "int",
                nullable: false,
                defaultValue: 5); // los registros existentes quedan con IVA 21% (código 5 de ARCA)

            migrationBuilder.CreateTable(
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VentaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PuntoVenta = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    Cae = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    VencimientoCae = table.Column<DateOnly>(type: "date", nullable: true),
                    DocTipo = table.Column<int>(type: "int", nullable: false),
                    DocNumero = table.Column<long>(type: "bigint", nullable: false),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceptorCondicionIva = table.Column<int>(type: "int", nullable: false),
                    ImporteNeto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteExento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteIva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Entorno = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facturas_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacturasIva",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    Alicuota = table.Column<int>(type: "int", nullable: false),
                    BaseImponible = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasIva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturasIva_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 1,
                column: "AlicuotaIva",
                value: 5);

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 2,
                column: "AlicuotaIva",
                value: 5);

            migrationBuilder.UpdateData(
                table: "Productos",
                keyColumn: "Id",
                keyValue: 3,
                column: "AlicuotaIva",
                value: 5);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Entorno_PuntoVenta_Tipo_Numero",
                table: "Facturas",
                columns: new[] { "Entorno", "PuntoVenta", "Tipo", "Numero" },
                unique: true,
                filter: "[Estado] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Estado",
                table: "Facturas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_VentaId",
                table: "Facturas",
                column: "VentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasIva_FacturaId",
                table: "FacturasIva",
                column: "FacturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturasIva");

            migrationBuilder.DropTable(
                name: "Facturas");

            migrationBuilder.DropColumn(
                name: "AlicuotaIva",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "AlicuotaIva",
                table: "DetallesVenta");
        }
    }
}
