using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
	public void Configure(EntityTypeBuilder<Categoria> builder)
	{
		builder.ToTable("Categorias");
		builder.HasKey(c => c.Id);

		builder.Property(c => c.Nombre)
			   .IsRequired()
			   .HasMaxLength(100);

		builder.HasIndex(c => c.Nombre).IsUnique();

		// Datos iniciales para poder probar
		builder.HasData(
			new Categoria { Id = 1, Nombre = "Almacén" },
			new Categoria { Id = 2, Nombre = "Bebidas" },
			new Categoria { Id = 3, Nombre = "Limpieza" }
		);
	}
}