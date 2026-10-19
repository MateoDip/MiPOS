using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class FacturaConfiguracion : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        builder.ToTable("Facturas");
        builder.HasKey(f => f.Id);

        // Códigos de ARCA guardados como número
        builder.Property(f => f.Tipo).HasConversion<int>();
        builder.Property(f => f.Estado).HasConversion<int>();
        builder.Property(f => f.ReceptorCondicionIva).HasConversion<int>();

        builder.Property(f => f.Cae).HasMaxLength(14);
        builder.Property(f => f.ReceptorNombre).HasMaxLength(150);
        builder.Property(f => f.Entorno).IsRequired().HasMaxLength(20);
        builder.Property(f => f.Mensaje).HasMaxLength(1000);

        builder.Property(f => f.ImporteNeto).HasPrecision(18, 2);
        builder.Property(f => f.ImporteExento).HasPrecision(18, 2);
        builder.Property(f => f.ImporteIva).HasPrecision(18, 2);
        builder.Property(f => f.Total).HasPrecision(18, 2);

        // Una venta tiene como máximo una factura
        builder.HasOne(f => f.Venta)
               .WithOne(v => v.Factura)
               .HasForeignKey<Factura>(f => f.VentaId)
               .OnDelete(DeleteBehavior.Restrict);

        // Un número autorizado no se puede repetir (por entorno: la numeración de
        // homologación y la simulada no tienen nada que ver con la real)
        builder.HasIndex(f => new { f.Entorno, f.PuntoVenta, f.Tipo, f.Numero })
               .IsUnique()
               .HasFilter("[Estado] = 2");

        // Para encontrar rápido las pendientes
        builder.HasIndex(f => f.Estado);
    }
}
