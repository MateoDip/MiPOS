using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class ProveedorConfiguracion : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("Proveedores");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre)
               .IsRequired()
               .HasMaxLength(150);

        // No puede haber dos proveedores con el mismo nombre
        builder.HasIndex(p => p.Nombre).IsUnique();

        // 11 dígitos, sin guiones
        builder.Property(p => p.Cuit).HasMaxLength(11);
        builder.Property(p => p.Telefono).HasMaxLength(50);
    }
}
