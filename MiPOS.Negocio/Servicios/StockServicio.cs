using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Productos;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

/// <summary>
/// Ajustes de stock a mano, alertas de stock bajo e historia de movimientos.
/// El descuento por ventas lo hace VentaServicio, y la suma por compras CompraServicio,
/// los dos con StockServicio.RegistrarMovimientoAsync dentro de su propia transacción.
/// </summary>
public class StockServicio
{
    public const int LargoMaximoMotivo = 200;

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;

    public StockServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    /// <summary>
    /// Suma (diferencia positiva) o resta (negativa) unidades a mano, con un motivo.
    /// Ej: -2 "Rotura", +10 "Mercadería encontrada en depósito".
    /// </summary>
    public async Task AjustarAsync(int productoId, int diferencia, string motivo)
    {
        var usuario = _sesion.ExigirUsuario();
        _sesion.Exigir(Permiso.GestionarStock);

        if (diferencia == 0)
            throw new ProductoInvalidoException("La cantidad a ajustar no puede ser cero.");

        motivo = ValidarMotivo(motivo);

        await using var contexto = await _fabrica.CreateDbContextAsync();
        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        await ExigirProductoAsync(contexto, productoId);
        await RegistrarMovimientoAsync(contexto, productoId, TipoMovimientoStock.Ajuste, diferencia,
            motivo, usuario.Id);

        await contexto.SaveChangesAsync();
        await transaccion.CommitAsync();
    }

    /// <summary>
    /// Deja el stock en lo que se contó en el local (ej: inventario). Registra un ajuste
    /// por la diferencia. Si el stock cambió mientras tanto (otra caja vendió), da error
    /// para que se vuelva a mirar antes de pisar el dato.
    /// </summary>
    public async Task EstablecerStockAsync(int productoId, int stockLeido, int stockContado, string motivo)
    {
        var usuario = _sesion.ExigirUsuario();
        _sesion.Exigir(Permiso.GestionarStock);

        if (stockContado < 0)
            throw new ProductoInvalidoException("El stock contado no puede ser negativo.");

        motivo = ValidarMotivo(motivo);

        var diferencia = stockContado - stockLeido;
        if (diferencia == 0)
            throw new ProductoInvalidoException("El stock contado es igual al actual: no hay nada que ajustar.");

        await using var contexto = await _fabrica.CreateDbContextAsync();
        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        // Solo se cambia si el stock sigue siendo el que vio el usuario
        var filas = await contexto.Productos
            .Where(p => p.Id == productoId && p.Stock == stockLeido)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stockContado));

        if (filas == 0)
        {
            await ExigirProductoAsync(contexto, productoId);
            throw new ProductoInvalidoException(
                "El stock del producto cambió mientras tanto (por ejemplo, una venta en otra caja). Vuelva a cargarlo y cuente de nuevo.");
        }

        contexto.MovimientosStock.Add(new MovimientoStock
        {
            ProductoId = productoId,
            Tipo = TipoMovimientoStock.Ajuste,
            Cantidad = diferencia,
            Motivo = motivo,
            Fecha = DateTime.Now,
            UsuarioId = usuario.Id
        });

        await contexto.SaveChangesAsync();
        await transaccion.CommitAsync();
    }

    // Productos activos con stock negativo o en su mínimo (o por debajo), los más urgentes primero
    public async Task<List<Producto>> ObtenerBajoMinimoAsync()
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Where(EstaBajoMinimo)
            .OrderBy(p => p.Stock - p.StockMinimo)
            .ThenBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<int> ContarBajoMinimoAsync()
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();
        return await contexto.Productos.CountAsync(EstaBajoMinimo);
    }

    // Últimos movimientos de un producto, del más nuevo al más viejo
    public async Task<List<MovimientoStock>> ObtenerMovimientosAsync(int productoId, int maximo = 200)
    {
        _sesion.Exigir(Permiso.GestionarStock);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.MovimientosStock
            .AsNoTracking()
            .Include(m => m.Usuario)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.Id)
            .Take(maximo)
            .ToListAsync();
    }

    /// <summary>
    /// Cambia el stock y deja registrado el movimiento, usando el contexto (y la transacción)
    /// de quien lo llama, para que se guarde todo junto o nada.
    /// El stock se cambia con una sola instrucción en la base (Stock = Stock + cantidad):
    /// así dos cajas que venden el mismo producto a la vez no se pisan.
    /// El movimiento queda agregado al contexto: lo guarda el SaveChanges de quien llama.
    /// </summary>
    internal static async Task RegistrarMovimientoAsync(MiPOSDbContext contexto, int productoId,
        TipoMovimientoStock tipo, int cantidad, string? motivo, int usuarioId,
        int? ventaId = null, int? compraId = null)
    {
        await contexto.Productos
            .Where(p => p.Id == productoId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + cantidad));

        contexto.MovimientosStock.Add(new MovimientoStock
        {
            ProductoId = productoId,
            Tipo = tipo,
            Cantidad = cantidad,
            Motivo = motivo,
            Fecha = DateTime.Now,
            UsuarioId = usuarioId,
            VentaId = ventaId,
            CompraId = compraId
        });
    }

    // ----- Ayudas -----

    // Se escribe así (y no como método) para que la base de datos pueda hacer el filtro
    private static readonly System.Linq.Expressions.Expression<Func<Producto, bool>> EstaBajoMinimo =
        p => p.Activo && (p.Stock < 0 || (p.StockMinimo > 0 && p.Stock <= p.StockMinimo));

    private static async Task ExigirProductoAsync(MiPOSDbContext contexto, int productoId)
    {
        if (!await contexto.Productos.AnyAsync(p => p.Id == productoId))
            throw new ProductoInvalidoException("El producto no existe.");
    }

    private static string ValidarMotivo(string? motivo)
    {
        motivo = motivo?.Trim() ?? string.Empty;

        if (motivo.Length == 0)
            throw new ProductoInvalidoException("Ingrese el motivo del ajuste.");
        if (motivo.Length > LargoMaximoMotivo)
            throw new ProductoInvalidoException($"El motivo no puede tener más de {LargoMaximoMotivo} caracteres.");

        return motivo;
    }
}
