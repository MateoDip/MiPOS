using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiPOS.Datos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDescuentoAVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Descuento",
                table: "Ventas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DescuentoAutorizadoPorId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoPorcentaje",
                table: "Ventas",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Ventas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_DescuentoAutorizadoPorId",
                table: "Ventas",
                column: "DescuentoAutorizadoPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Usuarios_DescuentoAutorizadoPorId",
                table: "Ventas",
                column: "DescuentoAutorizadoPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Usuarios_DescuentoAutorizadoPorId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_DescuentoAutorizadoPorId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Descuento",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoAutorizadoPorId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoPorcentaje",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Ventas");
        }
    }
}
