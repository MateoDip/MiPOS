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
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<TurnoCaja> TurnosCaja => Set<TurnoCaja>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<DetalleCompra> DetallesCompra => Set<DetalleCompra>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Busca automáticamente todas las clases de la carpeta Configuraciones
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiPOSDbContext).Assembly);
    }
}