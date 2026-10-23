using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiPOS.Datos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIdiomaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Idioma",
                table: "Usuarios",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Idioma",
                table: "Usuarios");
        }
    }
}
