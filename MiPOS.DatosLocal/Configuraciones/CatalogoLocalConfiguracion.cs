using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.DatosLocal.Configuraciones;

// Copia de los productos activos. Los Id son los de la base central (no se generan acá).
public class ProductoLocalConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Productos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasIndex(p => p.CodigoBarras).IsUnique();
        builder.HasIndex(p => p.Nombre);
        builder.Property(p => p.AlicuotaIva).HasConversion<int>();

        // Las categorías no hacen falta para vender
        builder.Ignore(p => p.Categoria);
    }
}

public class MedioPagoLocalConfiguracion : IEntityTypeConfiguration<MedioPago>
{
    public void Configure(EntityTypeBuilder<MedioPago> builder)
    {
        builder.ToTable("MediosPago");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
    }
}

// Usuarios activos, con el hash de la contraseña (nunca la contraseña), para iniciar sesión sin conexión
public class UsuarioLocalConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.HasIndex(u => u.NombreUsuario).IsUnique();
        builder.Property(u => u.Rol).HasConversion<int>();
    }
}
