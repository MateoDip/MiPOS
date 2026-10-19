using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Comun;
using MiPOS.Negocio.Compras;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

/// <summary>
/// Proveedores y compras de mercadería. Registrar una compra suma el stock de cada producto.
/// Las compras no se anulan: un error se corrige con un ajuste de stock.
/// </summary>
public class CompraServicio
{
    public const int LargoMaximoNombreProveedor = 150;
    public const int LargoMaximoTelefono = 50;
    public const int LargoMaximoComprobante = 50;

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;

    public CompraServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    // ----- Proveedores -----

    public async Task<List<Proveedor>> ObtenerProveedoresAsync(bool incluirInactivos)
    {
        _sesion.Exigir(Permiso.GestionarCompras);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Proveedores
            .AsNoTracking()
            .Where(p => incluirInactivos || p.Activo)
            .OrderBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<Proveedor> CrearProveedorAsync(string nombre, string? cuit, string? telefono)
    {
        _sesion.Exigir(Permiso.GestionarCompras);

        var proveedor = new Proveedor();
        AsignarDatos(proveedor, nombre, cuit, telefono);

        await using var contexto = await _fabrica.CreateDbContextAsync();
        await ValidarNombreLibreAsync(contexto, proveedor.Nombre, idActual: null);

        contexto.Proveedores.Add(proveedor);
        await GuardarProveedorAsync(contexto, proveedor.Nombre);
        return proveedor;
    }

    public async Task ModificarProveedorAsync(int id, string nombre, string? cuit, string? telefono)
    {
        _sesion.Exigir(Permiso.GestionarCompras);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var proveedor = await contexto.Proveedores.FirstOrDefaultAsync(p => p.Id == id)
                        ?? throw new CompraInvalidaException("El proveedor no existe.");

        AsignarDatos(proveedor, nombre, cuit, telefono);
        await ValidarNombreLibreAsync(contexto, proveedor.Nombre, idActual: id);
        await GuardarProveedorAsync(contexto, proveedor.Nombre);
    }

    public async Task CambiarActivoProveedorAsync(int id, bool activo)
    {
        _sesion.Exigir(Permiso.GestionarCompras);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var filas = await contexto.Proveedores
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, activo));

        if (filas == 0)
            throw new CompraInvalidaException("El proveedor no existe.");
    }

    // ----- Compras -----

    /// <summary>
    /// Guarda la compra y suma al stock cada producto, todo junto en una transacción.
    /// </summary>
    public async Task<Compra> RegistrarAsync(int proveedorId, string? numeroComprobante, IReadOnlyList<ItemCompra> items)
    {
        var usuario = _sesion.ExigirUsuario();
        _sesion.Exigir(Permiso.GestionarCompras);

        numeroComprobante = string.IsNullOrWhiteSpace(numeroComprobante) ? null : numeroComprobante.Trim();
        if (numeroComprobante?.Length > LargoMaximoComprobante)
            throw new CompraInvalidaException($"El número de comprobante no puede tener más de {LargoMaximoComprobante} caracteres.");

        if (items.Count == 0)
            throw new CompraInvalidaException("Agregue al menos un producto.");

        foreach (var item in items)
        {
            if (item.Cantidad <= 0)
                throw new CompraInvalidaException($"La cantidad de {item.NombreProducto} tiene que ser mayor a cero.");
            if (item.CostoUnitario < 0 || decimal.Round(item.CostoUnitario, 2) != item.CostoUnitario)
                throw new CompraInvalidaException($"El costo de {item.NombreProducto} es inválido (no negativo, hasta 2 decimales).");
        }

        if (items.Select(i => i.ProductoId).Distinct().Count() != items.Count)
            throw new CompraInvalidaException("Hay productos repetidos en la compra: sume las cantidades en una sola línea.");

        await using var contexto = await _fabrica.CreateDbContextAsync();

        if (!await contexto.Proveedores.AnyAsync(p => p.Id == proveedorId && p.Activo))
            throw new CompraInvalidaException("Elija un proveedor activo.");

        var ids = items.Select(i => i.ProductoId).ToList();
        var existentes = await contexto.Productos.CountAsync(p => ids.Contains(p.Id));
        if (existentes != ids.Count)
            throw new CompraInvalidaException("Uno de los productos de la compra no existe.");

        var compra = new Compra
        {
            Fecha = DateTime.Now,
            ProveedorId = proveedorId,
            NumeroComprobante = numeroComprobante,
            UsuarioId = usuario.Id,
            Total = items.Sum(i => i.Subtotal),
            Detalles = items.Select(i => new DetalleCompra
            {
                ProductoId = i.ProductoId,
                NombreProducto = i.NombreProducto,
                Cantidad = i.Cantidad,
                CostoUnitario = i.CostoUnitario,
                Subtotal = i.Subtotal
            }).ToList()
        };

        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        contexto.Compras.Add(compra);
        await contexto.SaveChangesAsync(); // acá la compra recibe su Id

        foreach (var detalle in compra.Detalles)
            await StockServicio.RegistrarMovimientoAsync(contexto, detalle.ProductoId, TipoMovimientoStock.Compra,
                detalle.Cantidad, motivo: null, usuario.Id, compraId: compra.Id);

        await contexto.SaveChangesAsync();
        await transaccion.CommitAsync();

        return compra;
    }

    // Las últimas compras, de la más nueva a la más vieja, con sus detalles
    public async Task<List<Compra>> ObtenerComprasAsync(int maximo = 200)
    {
        _sesion.Exigir(Permiso.GestionarCompras);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Compras
            .AsNoTracking()
            .Include(c => c.Proveedor)
            .Include(c => c.Usuario)
            .Include(c => c.Detalles.OrderBy(d => d.Id))
            .OrderByDescending(c => c.Id)
            .Take(maximo)
            .AsSplitQuery()
            .ToListAsync();
    }

    // Lo que costó el producto en su última compra (para proponerlo en la siguiente). null si nunca se compró.
    public async Task<decimal?> ObtenerUltimoCostoAsync(int productoId)
    {
        _sesion.Exigir(Permiso.GestionarCompras);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.DetallesCompra
            .Where(d => d.ProductoId == productoId)
            .OrderByDescending(d => d.Id)
            .Select(d => (decimal?)d.CostoUnitario)
            .FirstOrDefaultAsync();
    }

    // ----- Reglas y ayudas -----

    private static void AsignarDatos(Proveedor proveedor, string? nombre, string? cuit, string? telefono)
    {
        nombre = nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
            throw new CompraInvalidaException("Ingrese el nombre del proveedor.");
        if (nombre.Length > LargoMaximoNombreProveedor)
            throw new CompraInvalidaException($"El nombre no puede tener más de {LargoMaximoNombreProveedor} caracteres.");

        string? cuitNormalizado = null;
        if (!string.IsNullOrWhiteSpace(cuit))
        {
            if (!Cuit.IntentarNormalizar(cuit, out var valido))
                throw new CompraInvalidaException("El CUIT es inválido: tienen que ser 11 números (con o sin guiones) y el último es el verificador.");
            cuitNormalizado = valido;
        }

        telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        if (telefono?.Length > LargoMaximoTelefono)
            throw new CompraInvalidaException($"El teléfono no puede tener más de {LargoMaximoTelefono} caracteres.");

        proveedor.Nombre = nombre;
        proveedor.Cuit = cuitNormalizado;
        proveedor.Telefono = telefono;
    }

    private static async Task ValidarNombreLibreAsync(MiPOSDbContext contexto, string nombre, int? idActual)
    {
        if (await contexto.Proveedores.AnyAsync(p => p.Nombre == nombre && p.Id != idActual))
            throw new CompraInvalidaException($"Ya existe un proveedor llamado \"{nombre}\".");
    }

    private static async Task GuardarProveedorAsync(MiPOSDbContext contexto, string nombre)
    {
        try
        {
            await contexto.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // El índice único lo rechazó: otra PC lo cargó recién
            throw new CompraInvalidaException($"Ya existe un proveedor llamado \"{nombre}\".");
        }
    }
}
