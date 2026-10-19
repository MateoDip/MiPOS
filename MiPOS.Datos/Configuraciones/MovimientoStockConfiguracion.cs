using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class MovimientoStockConfiguracion : IEntityTypeConfiguration<MovimientoStock>
{
    public void Configure(EntityTypeBuilder<MovimientoStock> builder)
    {
        builder.ToTable("MovimientosStock");
        builder.HasKey(m => m.Id);

        // El tipo se guarda como número (Venta = 1, Compra = 2, Ajuste = 3)
        builder.Property(m => m.Tipo).HasConversion<int>();

        builder.Property(m => m.Motivo).HasMaxLength(200);

        // Para ver rápido la historia de un producto, ordenada por fecha
        builder.HasIndex(m => new { m.ProductoId, m.Fecha });

        // Restrict: no se borra un producto, usuario o venta que tenga movimientos
        builder.HasOne(m => m.Producto)
               .WithMany()
               .HasForeignKey(m => m.ProductoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Usuario)
               .WithMany()
               .HasForeignKey(m => m.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Venta)
               .WithMany()
               .HasForeignKey(m => m.VentaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Compra)
               .WithMany()
               .HasForeignKey(m => m.CompraId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
