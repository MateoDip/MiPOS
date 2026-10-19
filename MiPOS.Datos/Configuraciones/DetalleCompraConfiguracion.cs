using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class DetalleCompraConfiguracion : IEntityTypeConfiguration<DetalleCompra>
{
    public void Configure(EntityTypeBuilder<DetalleCompra> builder)
    {
        builder.ToTable("DetallesCompra");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.NombreProducto)
               .IsRequired()
               .HasMaxLength(150);

        builder.Property(d => d.CostoUnitario).HasPrecision(18, 2);
        builder.Property(d => d.Subtotal).HasPrecision(18, 2);

        // Cascade: los detalles son parte de la compra (igual las compras no se borran)
        builder.HasOne(d => d.Compra)
               .WithMany(c => c.Detalles)
               .HasForeignKey(d => d.CompraId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict: no se puede borrar un producto que figure en una compra
        builder.HasOne(d => d.Producto)
               .WithMany()
               .HasForeignKey(d => d.ProductoId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
