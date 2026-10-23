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
    // 2: Usuario.Idioma (Fase 10)
    public const int VersionEsquema = 2;

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
    /// Crea la base local si no existe. Si existe con otra versión de la estructura, la vuelve a crear
    /// (la copia del catálogo se descarga de nuevo) conservando las ventas pendientes de subir:
    /// la tabla VentasPendientes no cambia entre versiones.
    /// </summary>
    public void Preparar()
    {
        if (Database.EnsureCreated())
        {
            GuardarVersion();
            return;
        }

        int? version = null;
        try
        {
            version = int.TryParse(Valores.AsNoTracking().FirstOrDefault(v => v.Clave == ClaveVersion)?.Valor, out var leida)
                ? leida : null;
        }
        catch (Exception)
        {
            // La estructura es tan distinta que ni siquiera se puede leer: se recrea
        }

        if (version == VersionEsquema)
            return;

        // Nunca se pierden ventas sin subir: se leen antes de recrear y se vuelven a guardar.
        // Si no se pueden leer, no se toca nada (mejor una caja sin modo sin conexión que perder ventas).
        List<VentaPendiente> pendientes;
        try
        {
            pendientes = VentasPendientes.AsNoTracking().ToList();
        }
        catch (Exception)
        {
            pendientes = new();
            if (version is not null)
                throw;
        }

        Database.EnsureDeleted();
        Database.EnsureCreated();
        VentasPendientes.AddRange(pendientes);
        GuardarVersion();
    }

    private void GuardarVersion()
    {
        Valores.Add(new ValorLocal { Clave = ClaveVersion, Valor = VersionEsquema.ToString() });
        SaveChanges();
    }
}
