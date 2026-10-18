using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class CajaConfiguracion : IEntityTypeConfiguration<Caja>
{
    public void Configure(EntityTypeBuilder<Caja> builder)
    {
        builder.ToTable("Cajas");
        builder.HasKey(c => c.Id);

        // No puede haber dos cajas con el mismo número
        builder.HasIndex(c => c.Numero).IsUnique();

        builder.Property(c => c.Nombre)
               .IsRequired()
               .HasMaxLength(50);

        // Toda instalación empieza con una caja (appsettings.json trae Caja:Numero = 1)
        builder.HasData(new Caja { Id = 1, Numero = 1, Nombre = "Caja 1" });
    }
}
