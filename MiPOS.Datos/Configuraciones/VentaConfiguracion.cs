using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class VentaConfiguracion : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> builder)
    {
        builder.ToTable("Ventas");
        builder.HasKey(v => v.Id);

        // Índice para buscar ventas por fecha (reportes, cierre de caja)
        builder.HasIndex(v => v.Fecha);

        // Dinero: 18 dígitos en total, 2 decimales (centavos)
        builder.Property(v => v.Total).HasPrecision(18, 2);
        builder.Property(v => v.MontoRecibido).HasPrecision(18, 2);
        builder.Property(v => v.Vuelto).HasPrecision(18, 2);

        // Restrict: no se puede borrar un medio de pago que tenga ventas
        builder.HasOne(v => v.MedioPago)
               .WithMany()
               .HasForeignKey(v => v.MedioPagoId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
