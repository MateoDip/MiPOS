using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Productos;
using MiPOS.Negocio.SinConexion;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

public class ProductoServicio
{
    public const int LargoMaximoCodigo = 50;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoCategoria = 100;

    // Prefijo de los códigos internos de los artículos sin código de barras (INT0001, INT0002...)
    public const string PrefijoCodigoInterno = "INT";

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;
    private readonly BaseLocal? _baseLocal;

    // baseLocal es opcional: sin ella (por ejemplo, en las pruebas) se trabaja solo con la base central
    public ProductoServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion, BaseLocal? baseLocal = null)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _baseLocal = baseLocal;
    }

    // ----- Búsquedas de la caja (solo productos activos) -----

    public async Task<Producto?> BuscarPorCodigoAsync(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;

        codigo = codigo.Trim();

        // Sin conexión con la base central se busca en la copia de esta caja
        if (_baseLocal?.UsarLocal == true)
            return await BuscarPorCodigoLocalAsync(codigo);

        try
        {
            // Se abre una conexión solo para esta búsqueda y se cierra al terminar
            await using var contexto = await _fabrica.CreateDbContextAsync();

            return await contexto.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.CodigoBarras == codigo && p.Activo);
        }
        catch (Exception ex) when (_baseLocal?.EsCorte(ex) == true)
        {
            return await BuscarPorCodigoLocalAsync(codigo);
        }
    }

    private async Task<Producto?> BuscarPorCodigoLocalAsync(string codigo)
    {
        // Los códigos se guardan en mayúsculas. SQL Server no distingue mayúsculas al comparar, pero SQLite sí:
        // sin esto, "int0001" escrito a mano se encontraría con conexión y no sin ella.
        codigo = codigo.ToUpperInvariant();

        await using var local = await _baseLocal!.CrearContextoAsync();
        return await local.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.CodigoBarras == codigo && p.Activo);
    }

    public async Task<List<Producto>> BuscarPorNombreAsync(string texto, int maximo = 20)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2)
            return new List<Producto>();

        texto = texto.Trim();

        if (_baseLocal?.UsarLocal == true)
            return await BuscarPorNombreLocalAsync(texto, maximo);

        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync();

            return await contexto.Productos
                .AsNoTracking()
                .Where(p => p.Activo && p.Nombre.Contains(texto))
                .OrderBy(p => p.Nombre)
                .Take(maximo)
                .ToListAsync();
        }
        catch (Exception ex) when (_baseLocal?.EsCorte(ex) == true)
        {
            return await BuscarPorNombreLocalAsync(texto, maximo);
        }
    }

    // En SQLite, LIKE no distingue mayúsculas de minúsculas (Contains sí las distinguiría)
    private async Task<List<Producto>> BuscarPorNombreLocalAsync(string texto, int maximo)
    {
        await using var local = await _baseLocal!.CrearContextoAsync();
        return await local.Productos
            .AsNoTracking()
            .Where(p => p.Activo && EF.Functions.Like(p.Nombre, $"%{texto}%"))
            .OrderBy(p => p.Nombre)
            .Take(maximo)
            .ToListAsync();
    }

    // ----- Gestión de productos (solo administrador) -----

    /// <summary>
    /// Lista para la pantalla de productos. Busca por nombre o código (vacío = todos).
    /// Incluye los desactivados solo si se pide.
    /// </summary>
    public async Task<List<Producto>> ObtenerParaGestionAsync(string? texto, bool incluirInactivos, int maximo = 500)
    {
        _sesion.Exigir(Permiso.GestionarProductos);

        texto = texto?.Trim() ?? string.Empty;

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var consulta = contexto.Productos.AsNoTracking().Include(p => p.Categoria).AsQueryable();

        if (!incluirInactivos)
            consulta = consulta.Where(p => p.Activo);

        if (texto.Length > 0)
            consulta = consulta.Where(p => p.Nombre.Contains(texto) || p.CodigoBarras.Contains(texto));

        return await consulta
            .OrderBy(p => p.Nombre)
            .Take(maximo)
            .ToListAsync();
    }

    public async Task<Producto> ObtenerAsync(int id)
    {
        _sesion.Exigir(Permiso.GestionarProductos);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Productos.AsNoTracking().Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id)
               ?? throw new ProductoInvalidoException("El producto no existe.");
    }

    /// <summary>
    /// Da de alta un producto. Si stockInicial es mayor a cero, queda registrado
    /// como un ajuste "Stock inicial" (el stock solo cambia con movimientos).
    /// </summary>
    public async Task<Producto> CrearAsync(DatosProducto datos, int stockInicial)
    {
        var usuario = _sesion.ExigirUsuario();
        _sesion.Exigir(Permiso.GestionarProductos);

        if (stockInicial < 0)
            throw new ProductoInvalidoException("El stock inicial no puede ser negativo.");
        if (stockInicial > 0)
            _sesion.Exigir(Permiso.GestionarStock);

        datos = Normalizar(datos);
        Validar(datos);

        await using var contexto = await _fabrica.CreateDbContextAsync();
        await ValidarEnBaseAsync(contexto, datos, idActual: null);

        var producto = new Producto
        {
            CodigoBarras = datos.CodigoBarras,
            Nombre = datos.Nombre,
            CategoriaId = datos.CategoriaId,
            Precio = datos.Precio,
            StockMinimo = datos.StockMinimo,
            AlicuotaIva = datos.AlicuotaIva
        };

        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        contexto.Productos.Add(producto);
        await GuardarAsync(contexto, datos.CodigoBarras);

        if (stockInicial > 0)
        {
            await StockServicio.RegistrarMovimientoAsync(contexto, producto.Id, TipoMovimientoStock.Ajuste,
                stockInicial, "Stock inicial", usuario.Id);
            await contexto.SaveChangesAsync();
            producto.Stock = stockInicial;
        }

        await transaccion.CommitAsync();
        return producto;
    }

    // Cambiar el precio requiere además el permiso ModificarPrecios
    public async Task ModificarAsync(int id, DatosProducto datos)
    {
        _sesion.Exigir(Permiso.GestionarProductos);

        datos = Normalizar(datos);
        Validar(datos);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var producto = await contexto.Productos.FirstOrDefaultAsync(p => p.Id == id)
                       ?? throw new ProductoInvalidoException("El producto no existe.");

        if (producto.Precio != datos.Precio)
            _sesion.Exigir(Permiso.ModificarPrecios);

        await ValidarEnBaseAsync(contexto, datos, idActual: id);

        producto.CodigoBarras = datos.CodigoBarras;
        producto.Nombre = datos.Nombre;
        producto.CategoriaId = datos.CategoriaId;
        producto.Precio = datos.Precio;
        producto.StockMinimo = datos.StockMinimo;
        producto.AlicuotaIva = datos.AlicuotaIva;

        await GuardarAsync(contexto, datos.CodigoBarras);
    }

    // Los productos no se borran: se desactivan (dejan de aparecer en la caja)
    public async Task CambiarActivoAsync(int id, bool activo)
    {
        _sesion.Exigir(Permiso.GestionarProductos);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var filas = await contexto.Productos
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, activo));

        if (filas == 0)
            throw new ProductoInvalidoException("El producto no existe.");
    }

    /// <summary>
    /// Próximo código interno libre para un artículo sin código de barras (ej: INT0007).
    /// Es una sugerencia: si dos PC lo piden a la vez, el índice único evita que se repita al guardar.
    /// </summary>
    public async Task<string> GenerarCodigoInternoAsync()
    {
        _sesion.Exigir(Permiso.GestionarProductos);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var codigos = await contexto.Productos
            .Where(p => p.CodigoBarras.StartsWith(PrefijoCodigoInterno))
            .Select(p => p.CodigoBarras)
            .ToListAsync();

        var ultimo = codigos
            .Select(c => int.TryParse(c[PrefijoCodigoInterno.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{PrefijoCodigoInterno}{ultimo + 1:0000}";
    }

    // ----- Categorías -----

    public async Task<List<Categoria>> ObtenerCategoriasAsync()
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Categorias
            .AsNoTracking()
            .Where(c => c.Activa)
            .OrderBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task<Categoria> CrearCategoriaAsync(string nombre)
    {
        _sesion.Exigir(Permiso.GestionarProductos);

        nombre = nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
            throw new ProductoInvalidoException("Ingrese el nombre de la categoría.");
        if (nombre.Length > LargoMaximoCategoria)
            throw new ProductoInvalidoException($"El nombre de la categoría no puede tener más de {LargoMaximoCategoria} caracteres.");

        await using var contexto = await _fabrica.CreateDbContextAsync();

        // Si existía desactivada, se vuelve a activar en lugar de crear otra igual
        var existente = await contexto.Categorias.FirstOrDefaultAsync(c => c.Nombre == nombre);
        if (existente is not null)
        {
            if (existente.Activa)
                throw new ProductoInvalidoException($"Ya existe la categoría \"{existente.Nombre}\".");

            existente.Activa = true;
            await contexto.SaveChangesAsync();
            return existente;
        }

        var categoria = new Categoria { Nombre = nombre };
        contexto.Categorias.Add(categoria);
        await contexto.SaveChangesAsync();
        return categoria;
    }

    // ----- Reglas y ayudas -----

    private static DatosProducto Normalizar(DatosProducto datos)
        => datos with
        {
            CodigoBarras = (datos.CodigoBarras ?? string.Empty).Trim().ToUpperInvariant(),
            Nombre = (datos.Nombre ?? string.Empty).Trim()
        };

    private static void Validar(DatosProducto datos)
    {
        if (datos.CodigoBarras.Length == 0)
            throw new ProductoInvalidoException("Ingrese el código de barras (o genere un código interno).");
        if (datos.CodigoBarras.Length > LargoMaximoCodigo)
            throw new ProductoInvalidoException($"El código no puede tener más de {LargoMaximoCodigo} caracteres.");
        if (!datos.CodigoBarras.All(c => char.IsLetterOrDigit(c) || c is '-' or '.'))
            throw new ProductoInvalidoException("El código solo puede tener letras, números, punto y guion (sin espacios).");

        if (datos.Nombre.Length == 0)
            throw new ProductoInvalidoException("Ingrese el nombre del producto.");
        if (datos.Nombre.Length > LargoMaximoNombre)
            throw new ProductoInvalidoException($"El nombre no puede tener más de {LargoMaximoNombre} caracteres.");

        if (datos.Precio <= 0)
            throw new ProductoInvalidoException("El precio tiene que ser mayor a cero.");
        if (decimal.Round(datos.Precio, 2) != datos.Precio)
            throw new ProductoInvalidoException("El precio puede tener como máximo 2 decimales.");

        if (datos.StockMinimo < 0)
            throw new ProductoInvalidoException("El stock mínimo no puede ser negativo.");

        if (!Enum.IsDefined(datos.AlicuotaIva))
            throw new ProductoInvalidoException("Elija el IVA del producto.");
    }

    private static async Task ValidarEnBaseAsync(MiPOSDbContext contexto, DatosProducto datos, int? idActual)
    {
        if (!await contexto.Categorias.AnyAsync(c => c.Id == datos.CategoriaId && c.Activa))
            throw new ProductoInvalidoException("Elija una categoría.");

        var repetido = await contexto.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CodigoBarras == datos.CodigoBarras && p.Id != idActual);

        if (repetido is not null)
            throw new ProductoInvalidoException(MensajeCodigoRepetido(datos.CodigoBarras, repetido));
    }

    private static async Task GuardarAsync(MiPOSDbContext contexto, string codigo)
    {
        try
        {
            await contexto.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // El índice único lo rechazó: otra PC lo cargó recién con el mismo código
            throw new ProductoInvalidoException($"Ya existe un producto con el código {codigo}.");
        }
    }

    private static string MensajeCodigoRepetido(string codigo, Producto repetido)
        => repetido.Activo
            ? $"Ya existe un producto con el código {codigo}: {repetido.Nombre}."
            : $"Ya existe un producto desactivado con el código {codigo}: {repetido.Nombre}. Actívelo en lugar de crear uno nuevo.";
}
