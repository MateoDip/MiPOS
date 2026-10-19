using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class CompraConfiguracion : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> builder)
    {
        builder.ToTable("Compras");
        builder.HasKey(c => c.Id);

        // Para listar las compras por fecha
        builder.HasIndex(c => c.Fecha);

        builder.Property(c => c.NumeroComprobante).HasMaxLength(50);
        builder.Property(c => c.Total).HasPrecision(18, 2);

        // Restrict: no se borra un proveedor o usuario que tenga compras
        builder.HasOne(c => c.Proveedor)
               .WithMany()
               .HasForeignKey(c => c.ProveedorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
               .WithMany()
               .HasForeignKey(c => c.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
