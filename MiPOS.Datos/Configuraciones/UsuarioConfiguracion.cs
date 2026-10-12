using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.NombreUsuario)
               .IsRequired()
               .HasMaxLength(50);

        // No puede haber dos usuarios con el mismo nombre
        builder.HasIndex(u => u.NombreUsuario).IsUnique();

        builder.Property(u => u.NombreCompleto)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(u => u.ContrasenaHash)
               .IsRequired()
               .HasMaxLength(200);

        // El rol se guarda como número (Cajero = 1, Administrador = 2)
        builder.Property(u => u.Rol).HasConversion<int>();

        // No hay datos iniciales: el primer administrador se crea en el primer arranque
    }
}
