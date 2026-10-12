using Microsoft.EntityFrameworkCore;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos;

public class MiPOSDbContext : DbContext
{
    public MiPOSDbContext(DbContextOptions<MiPOSDbContext> options) : base(options) { }

    // Cada DbSet es una tabla
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<MedioPago> MediosPago => Set<MedioPago>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Busca automáticamente todas las clases de la carpeta Configuraciones
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiPOSDbContext).Assembly);
    }
}