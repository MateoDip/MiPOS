using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiPOS.Datos;

// Esta clase solo la usa "dotnet ef" al crear migraciones.
// El programa real tomará la conexión de un archivo de configuración (lo vemos más adelante).
public class MiPOSDbContextFactory : IDesignTimeDbContextFactory<MiPOSDbContext>
{
    public MiPOSDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MiPOSDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=MiPOS;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new MiPOSDbContext(options);
    }
}