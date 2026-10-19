using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class DetalleVentaConfiguracion : IEntityTypeConfiguration<DetalleVenta>
{
    public void Configure(EntityTypeBuilder<DetalleVenta> builder)
    {
        builder.ToTable("DetallesVenta");
        builder.HasKey(d => d.Id);

        // Mismo largo que Producto.Nombre
        builder.Property(d => d.NombreProducto)
               .IsRequired()
               .HasMaxLength(150);

        builder.Property(d => d.PrecioUnitario).HasPrecision(18, 2);
        builder.Property(d => d.Subtotal).HasPrecision(18, 2);
        builder.Property(d => d.AlicuotaIva).HasConversion<int>();

        // Una venta tiene muchos detalles. Restrict: las ventas no se borran.
        builder.HasOne(d => d.Venta)
               .WithMany(v => v.Detalles)
               .HasForeignKey(d => d.VentaId)
               .OnDelete(DeleteBehavior.Restrict);

        // Restrict: no se puede borrar un producto que tenga ventas
        builder.HasOne(d => d.Producto)
               .WithMany()
               .HasForeignKey(d => d.ProductoId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
