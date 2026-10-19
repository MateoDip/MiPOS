using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class FacturaIvaConfiguracion : IEntityTypeConfiguration<FacturaIva>
{
    public void Configure(EntityTypeBuilder<FacturaIva> builder)
    {
        builder.ToTable("FacturasIva");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Alicuota).HasConversion<int>();
        builder.Property(i => i.BaseImponible).HasPrecision(18, 2);
        builder.Property(i => i.Importe).HasPrecision(18, 2);

        // Cascade: el detalle de IVA es parte de la factura (igual las facturas no se borran)
        builder.HasOne(i => i.Factura)
               .WithMany(f => f.Alicuotas)
               .HasForeignKey(i => i.FacturaId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
