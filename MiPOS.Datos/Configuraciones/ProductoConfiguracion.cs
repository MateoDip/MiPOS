using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Productos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CodigoBarras)
               .IsRequired()
               .HasMaxLength(50);

        // El código de barras no se puede repetir, y el índice
        // hace que la búsqueda al escanear sea muy rápida
        builder.HasIndex(p => p.CodigoBarras).IsUnique();

        builder.Property(p => p.Nombre)
               .IsRequired()
               .HasMaxLength(150);

        // Índice para acelerar la búsqueda por nombre
        builder.HasIndex(p => p.Nombre);

        // 18 dígitos en total, 2 decimales (centavos)
        builder.Property(p => p.Precio).HasPrecision(18, 2);

        // Un producto pertenece a una categoría.
        // Restrict: no se puede borrar una categoría que tenga productos.
        builder.HasOne(p => p.Categoria)
               .WithMany(c => c.Productos)
               .HasForeignKey(p => p.CategoriaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new Producto { Id = 1, CodigoBarras = "7791234", Nombre = "Coca Cola 2.25L", Precio = 2500m, CategoriaId = 2 },
            new Producto { Id = 2, CodigoBarras = "7795678", Nombre = "Arroz 1Kg", Precio = 1500m, CategoriaId = 1 },
            new Producto { Id = 3, CodigoBarras = "7798912", Nombre = "Detergente", Precio = 2200m, CategoriaId = 3 }
        );
    }
}