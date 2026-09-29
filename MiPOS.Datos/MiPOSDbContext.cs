using Microsoft.EntityFrameworkCore;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Datos;

public class MiPOSDbContext : DbContext
{
    public MiPOSDbContext(DbContextOptions<MiPOSDbContext> options) : base(options) { }

    // Cada DbSet es una tabla
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Busca automáticamente todas las clases de la carpeta Configuraciones
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiPOSDbContext).Assembly);
    }
}