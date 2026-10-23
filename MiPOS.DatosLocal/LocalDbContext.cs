using Microsoft.EntityFrameworkCore;
using MiPOS.Dominio.Entidades;

namespace MiPOS.DatosLocal;

/// <summary>
/// Base local (SQLite) de cada caja. Tiene:
/// - una copia del catálogo para vender sin conexión (productos, medios de pago, usuarios),
///   que se reemplaza completa en cada descarga;
/// - las ventas hechas sin conexión (VentasPendientes), que se borran de acá cuando se suben a la base central;
/// - datos sueltos (Valores): la caja, el turno abierto, la fecha de la última descarga.
/// No tiene migraciones: si cambia la estructura, se sube VersionEsquema y la base se vuelve a crear
/// (solo si no tiene ventas pendientes de subir).
/// </summary>
public class LocalDbContext : DbContext
{
    // Subir este número cuando cambie alguna tabla de la base local
    public const int VersionEsquema = 1;

    private const string ClaveVersion = "VersionEsquema";

    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MedioPago> MediosPago => Set<MedioPago>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<VentaPendiente> VentasPendientes => Set<VentaPendiente>();
    public DbSet<ValorLocal> Valores => Set<ValorLocal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Solo las configuraciones de la carpeta Configuraciones de este proyecto
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalDbContext).Assembly);
    }

    /// <summary>
    /// Crea la base local si no existe. Si existe con otra versión de la estructura y no tiene
    /// ventas pendientes, la vuelve a crear (la copia del catálogo se descarga de nuevo).
    /// </summary>
    public void Preparar()
    {
        if (Database.EnsureCreated())
        {
            GuardarVersion();
            return;
        }

        int? version = null;
        var hayPendientes = false;
        try
        {
            version = int.TryParse(Valores.Find(ClaveVersion)?.Valor, out var leida) ? leida : null;
            hayPendientes = VentasPendientes.Any();
        }
        catch (Exception)
        {
            // La estructura es tan distinta que ni siquiera se puede leer: se recrea
        }

        // Nunca se borran ventas sin subir
        if (version == VersionEsquema || hayPendientes)
            return;

        Database.EnsureDeleted();
        Database.EnsureCreated();
        GuardarVersion();
    }

    private void GuardarVersion()
    {
        Valores.Add(new ValorLocal { Clave = ClaveVersion, Valor = VersionEsquema.ToString() });
        SaveChanges();
    }
}
