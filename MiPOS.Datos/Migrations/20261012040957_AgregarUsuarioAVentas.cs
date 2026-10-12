using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiPOS.Datos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsuarioAVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las ventas hechas antes de la Fase 2 eran de prueba y no tienen usuario.
            // Se borran (decisión del equipo) para poder hacer obligatorio el UsuarioId.
            // Primero los detalles, porque dependen de la venta.
            migrationBuilder.Sql("DELETE FROM DetallesVenta;");
            migrationBuilder.Sql("DELETE FROM Ventas;");

            migrationBuilder.AddColumn<int>(
                name: "UsuarioId",
                table: "Ventas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_UsuarioId",
                table: "Ventas",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Usuarios_UsuarioId",
                table: "Ventas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Usuarios_UsuarioId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_UsuarioId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Ventas");
        }
    }
}
