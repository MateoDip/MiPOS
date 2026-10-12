using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Servicios;

public class ProductoServicio
{
    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;

    public ProductoServicio(IDbContextFactory<MiPOSDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task<Producto?> BuscarPorCodigoAsync(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;

        codigo = codigo.Trim();

        // Se abre una conexión solo para esta búsqueda y se cierra al terminar
        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.CodigoBarras == codigo && p.Activo);
    }

    public async Task<List<Producto>> BuscarPorNombreAsync(string texto, int maximo = 20)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2)
            return new List<Producto>();

        texto = texto.Trim();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Productos
            .AsNoTracking()
            .Where(p => p.Activo && p.Nombre.Contains(texto))
            .OrderBy(p => p.Nombre)
            .Take(maximo)
            .ToListAsync();
    }
}