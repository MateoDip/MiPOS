using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class MovimientoCajaConfiguracion : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("MovimientosCaja");
        builder.HasKey(m => m.Id);

        // El tipo se guarda como número (Ingreso = 1, Retiro = 2)
        builder.Property(m => m.Tipo).HasConversion<int>();

        builder.Property(m => m.Monto).HasPrecision(18, 2);

        builder.Property(m => m.Motivo)
               .IsRequired()
               .HasMaxLength(200);

        // Restrict: no se borra un turno que tenga movimientos
        builder.HasOne(m => m.TurnoCaja)
               .WithMany(t => t.Movimientos)
               .HasForeignKey(m => m.TurnoCajaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Usuario)
               .WithMany()
               .HasForeignKey(m => m.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AutorizadoPor)
               .WithMany()
               .HasForeignKey(m => m.AutorizadoPorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
