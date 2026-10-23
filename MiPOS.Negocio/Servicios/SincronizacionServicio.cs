using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.DatosLocal;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.SinConexion;

namespace MiPOS.Negocio.Servicios;

// Cómo quedó una sincronización: si hay conexión, cuántas ventas se subieron y cuántas faltan
public record ResultadoSincronizacion(bool HayConexion, int VentasSubidas, int VentasPendientes, string? UltimoError);

/// <summary>
/// Mantiene al día la base local de la caja con la base central:
/// 1. prueba la conexión;
/// 2. sube las ventas hechas sin conexión (cada una en su transacción, con su descuento de stock);
/// 3. cada tanto, descarga de nuevo la copia de productos, medios de pago y usuarios,
///    y siempre la caja y su turno abierto.
/// La pantalla de la caja lo llama cada SinConexion:SegundosEntreIntentos.
/// </summary>
public class SincronizacionServicio
{
    // Una sola sincronización a la vez en esta PC
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly BaseLocal _baseLocal;
    private readonly OpcionesCaja _opcionesCaja;
    private readonly FacturacionServicio _facturacion;

    public SincronizacionServicio(IDbContextFactory<MiPOSDbContext> fabrica, BaseLocal baseLocal,
                                  OpcionesCaja opcionesCaja, FacturacionServicio facturacion)
    {
        _fabrica = fabrica;
        _baseLocal = baseLocal;
        _opcionesCaja = opcionesCaja;
        _facturacion = facturacion;
    }

    public bool Habilitada => _baseLocal.Habilitada;

    public bool SinConexion => _baseLocal.SinConexion;

    public DateTime? SinConexionDesde => _baseLocal.SinConexionDesde;

    public TimeSpan Intervalo => TimeSpan.FromSeconds(Math.Max(5, _baseLocal.Opciones.SegundosEntreIntentos));

    public Task<int> ContarVentasPendientesAsync() => _baseLocal.ContarVentasPendientesAsync();

    /// <summary>
    /// Hace todo el ciclo. forzarDescarga = descargar el catálogo aunque no haya pasado el tiempo
    /// (por ejemplo, al abrir la caja). Nunca lanza errores: los informa en el resultado.
    /// </summary>
    public async Task<ResultadoSincronizacion> SincronizarAsync(bool forzarDescarga = false, CancellationToken cancelacion = default)
    {
        if (!Habilitada)
            return new ResultadoSincronizacion(true, 0, 0, null);

        await Candado.WaitAsync(cancelacion);
        try
        {
            var veniaSinConexion = _baseLocal.SinConexion;

            if (!await HayConexionAsync(cancelacion))
            {
                _baseLocal.MarcarSinConexion();
                return new ResultadoSincronizacion(false, 0, await ContarVentasPendientesAsync(), null);
            }

            var (subidas, error) = await SubirVentasPendientesAsync(cancelacion);

            // Las ventas subidas con factura pendiente se autorizan ahora (si falla, queda para Ctrl+F)
            if (subidas > 0 && _facturacion.EstaActiva)
            {
                try { await _facturacion.AutorizarPendientesAsync(cancelacion); }
                catch (Exception) { }
            }

            if (forzarDescarga || veniaSinConexion || await TocaDescargarAsync())
                await DescargarCatalogoAsync(cancelacion);
            else
                await DescargarCajaYTurnoAsync(cancelacion);

            _baseLocal.MarcarConConexion();
            return new ResultadoSincronizacion(true, subidas, await ContarVentasPendientesAsync(), error);
        }
        catch (Exception ex) when (ErroresConexion.EsErrorDeConexion(ex))
        {
            _baseLocal.MarcarSinConexion();
            return new ResultadoSincronizacion(false, 0, await ContarVentasPendientesSinErrorAsync(), null);
        }
        catch (Exception ex)
        {
            return new ResultadoSincronizacion(!_baseLocal.SinConexion, 0, await ContarVentasPendientesSinErrorAsync(), ex.Message);
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<bool> HayConexionAsync(CancellationToken cancelacion)
    {
        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion);
            return await contexto.Database.CanConnectAsync(cancelacion);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Sube las ventas hechas sin conexión, de la más vieja a la más nueva.
    /// Si una venta ya está en la base central (mismo IdentificadorUnico), solo se borra de la local.
    /// Si una falla por algo que no es la conexión, se anota el error y se sigue con las demás.
    /// </summary>
    private async Task<(int Subidas, string? UltimoError)> SubirVentasPendientesAsync(CancellationToken cancelacion)
    {
        await using var local = await _baseLocal.CrearContextoAsync();
        var pendientes = await local.VentasPendientes.OrderBy(v => v.Id).ToListAsync(cancelacion);

        var subidas = 0;
        string? ultimoError = null;

        foreach (var pendiente in pendientes)
        {
            try
            {
                await SubirAsync(pendiente, cancelacion);
                local.VentasPendientes.Remove(pendiente);
                await local.SaveChangesAsync(cancelacion);
                subidas++;
            }
            catch (Exception ex) when (!ErroresConexion.EsErrorDeConexion(ex))
            {
                pendiente.Intentos++;
                pendiente.UltimoError = (ex.InnerException ?? ex).Message;
                await local.SaveChangesAsync(cancelacion);
                ultimoError = $"No se pudo subir la venta sin conexión N° {pendiente.Id}: {pendiente.UltimoError}";
            }
        }

        return (subidas, ultimoError);
    }

    private async Task SubirAsync(VentaPendiente pendiente, CancellationToken cancelacion)
    {
        var venta = JsonSerializer.Deserialize<Venta>(pendiente.Datos, ValorLocal.OpcionesJson)
                    ?? throw new InvalidOperationException("Los datos guardados de la venta están dañados.");
        VentaServicio.LimpiarIds(venta);
        venta.IdentificadorUnico = pendiente.IdentificadorUnico;

        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion);

        // Ya estaba subida (por ejemplo, se cortó justo después de confirmar la subida anterior)
        if (await contexto.Ventas.AnyAsync(v => v.IdentificadorUnico == pendiente.IdentificadorUnico, cancelacion))
            return;

        // Igual que una venta común: la venta, sus detalles, su factura pendiente y el stock, todo junto.
        // No se vuelven a validar las reglas: la venta ya se cobró y el cliente se fue con la mercadería.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        contexto.Ventas.Add(venta);
        await contexto.SaveChangesAsync(cancelacion);

        foreach (var detalle in venta.Detalles)
            await StockServicio.RegistrarMovimientoAsync(contexto, detalle.ProductoId, TipoMovimientoStock.Venta,
                -detalle.Cantidad, "Venta sin conexión", venta.UsuarioId, venta.Id);

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
    }

    private async Task<bool> TocaDescargarAsync()
    {
        await using var local = await _baseLocal.CrearContextoAsync();
        var ultima = await local.LeerAsync<string>(BaseLocal.ClaveUltimaDescarga);

        return ultima is null
               || !DateTime.TryParse(ultima, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.RoundtripKind, out var fecha)
               || DateTime.UtcNow - fecha > TimeSpan.FromMinutes(Math.Max(1, _baseLocal.Opciones.MinutosEntreDescargas));
    }

    /// <summary>
    /// Reemplaza la copia local de productos activos, medios de pago activos y usuarios activos
    /// (con el hash de la contraseña, para iniciar sesión sin conexión). Todo junto o nada.
    /// </summary>
    private async Task DescargarCatalogoAsync(CancellationToken cancelacion)
    {
        List<Producto> productos;
        List<MedioPago> medios;
        List<Usuario> usuarios;

        await using (var contexto = await _fabrica.CreateDbContextAsync(cancelacion))
        {
            productos = await contexto.Productos.AsNoTracking().Where(p => p.Activo).ToListAsync(cancelacion);
            medios = await contexto.MediosPago.AsNoTracking().Where(m => m.Activo).ToListAsync(cancelacion);
            usuarios = await contexto.Usuarios.AsNoTracking().Where(u => u.Activo).ToListAsync(cancelacion);
        }

        // Los contadores de intentos fallidos son de la base central
        foreach (var usuario in usuarios)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHastaUtc = null;
        }

        await using (var local = await _baseLocal.CrearContextoAsync())
        {
            await using var transaccion = await local.Database.BeginTransactionAsync(cancelacion);

            await local.Productos.ExecuteDeleteAsync(cancelacion);
            await local.MediosPago.ExecuteDeleteAsync(cancelacion);
            await local.Usuarios.ExecuteDeleteAsync(cancelacion);

            local.Productos.AddRange(productos);
            local.MediosPago.AddRange(medios);
            local.Usuarios.AddRange(usuarios);
            await local.GuardarAsync(BaseLocal.ClaveUltimaDescarga, DateTime.UtcNow.ToString("O"));

            await local.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }

        await DescargarCajaYTurnoAsync(cancelacion);
    }

    // La caja de esta PC y su turno abierto (consulta chica: se hace en cada sincronización)
    private async Task DescargarCajaYTurnoAsync(CancellationToken cancelacion)
    {
        await using var contexto = await _fabrica.CreateDbContextAsync(cancelacion);

        var caja = await contexto.Cajas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Numero == _opcionesCaja.Numero && c.Activa, cancelacion);
        if (caja is null)
            return;

        var turno = await contexto.TurnosCaja.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == caja.Id && t.FechaCierre == null, cancelacion);

        await _baseLocal.GuardarCajaYTurnoAsync(caja, turno);
    }

    private async Task<int> ContarVentasPendientesSinErrorAsync()
    {
        try { return await ContarVentasPendientesAsync(); }
        catch (Exception) { return 0; }
    }
}
