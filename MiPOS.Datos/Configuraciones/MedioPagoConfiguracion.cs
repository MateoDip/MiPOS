using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class MedioPagoConfiguracion : IEntityTypeConfiguration<MedioPago>
{
    public void Configure(EntityTypeBuilder<MedioPago> builder)
    {
        builder.ToTable("MediosPago");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nombre)
               .IsRequired()
               .HasMaxLength(50);

        builder.HasIndex(m => m.Nombre).IsUnique();

        // Medios de pago de la especificación
        builder.HasData(
            new MedioPago { Id = 1, Nombre = "Efectivo", EsEfectivo = true },
            new MedioPago { Id = 2, Nombre = "Débito", UsaTerminal = true },
            new MedioPago { Id = 3, Nombre = "Crédito", UsaTerminal = true },
            new MedioPago { Id = 4, Nombre = "Mercado Pago", UsaTerminal = true },
            new MedioPago { Id = 5, Nombre = "QR" },
            new MedioPago { Id = 6, Nombre = "Transferencia" }
        );
    }
}
