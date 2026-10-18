using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos.Configuraciones;

public class TurnoCajaConfiguracion : IEntityTypeConfiguration<TurnoCaja>
{
    public void Configure(EntityTypeBuilder<TurnoCaja> builder)
    {
        builder.ToTable("TurnosCaja");
        builder.HasKey(t => t.Id);

        // EstaAbierto se calcula, no es una columna
        builder.Ignore(t => t.EstaAbierto);

        // Una caja no puede tener dos turnos abiertos a la vez. Lo controla la base,
        // así no pasa aunque dos PCs intenten abrir la misma caja al mismo tiempo.
        builder.HasIndex(t => t.CajaId)
               .IsUnique()
               .HasFilter("[FechaCierre] IS NULL");

        // Índice para buscar turnos por fecha (reportes)
        builder.HasIndex(t => t.FechaApertura);

        // Dinero: 18 dígitos en total, 2 decimales (centavos)
        builder.Property(t => t.MontoInicial).HasPrecision(18, 2);
        builder.Property(t => t.EfectivoEsperado).HasPrecision(18, 2);
        builder.Property(t => t.EfectivoContado).HasPrecision(18, 2);
        builder.Property(t => t.Diferencia).HasPrecision(18, 2);

        // Restrict: no se borran cajas ni usuarios que tengan turnos (se desactivan)
        builder.HasOne(t => t.Caja)
               .WithMany()
               .HasForeignKey(t => t.CajaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.UsuarioApertura)
               .WithMany()
               .HasForeignKey(t => t.UsuarioAperturaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.UsuarioCierre)
               .WithMany()
               .HasForeignKey(t => t.UsuarioCierreId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
