using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiPOS.DatosLocal.Configuraciones;

public class VentaPendienteConfiguracion : IEntityTypeConfiguration<VentaPendiente>
{
    public void Configure(EntityTypeBuilder<VentaPendiente> builder)
    {
        builder.ToTable("VentasPendientes");
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => v.IdentificadorUnico).IsUnique();
        builder.Property(v => v.Datos).IsRequired();
    }
}

public class ValorLocalConfiguracion : IEntityTypeConfiguration<ValorLocal>
{
    public void Configure(EntityTypeBuilder<ValorLocal> builder)
    {
        builder.ToTable("Valores");
        builder.HasKey(v => v.Clave);
    }
}
