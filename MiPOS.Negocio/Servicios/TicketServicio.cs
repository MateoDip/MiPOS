using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.SinConexion;
using MiPOS.Negocio.Ventas;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

public class TicketServicio
{
    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly DatosComercio _comercio;
    private readonly Sesion _sesion;
    private readonly CajaServicio _cajaServicio;
    private readonly DatosFiscales _datosFiscales;
    private readonly FacturacionServicio _facturacion;
    private readonly BaseLocal? _baseLocal;
    private readonly LicenciaServicio? _licencia;

    public TicketServicio(IDbContextFactory<MiPOSDbContext> fabrica, DatosComercio comercio, Sesion sesion,
                          CajaServicio cajaServicio, DatosFiscales datosFiscales, FacturacionServicio facturacion,
                          BaseLocal? baseLocal = null, LicenciaServicio? licencia = null)
    {
        _licencia = licencia;
        _fabrica = fabrica;
        _comercio = comercio;
        _sesion = sesion;
        _cajaServicio = cajaServicio;
        _datosFiscales = datosFiscales;
        _facturacion = facturacion;
        _baseLocal = baseLocal;
    }

    /// <summary>
    /// Ticket de una venta buscada por su IdentificadorUnico: si todavía está en la base local
    /// (hecha sin conexión y sin subir) sale como "venta sin conexión"; si ya se subió, sale con su número real.
    /// </summary>
    public async Task<TicketParaImprimir> ArmarPorIdentificadorAsync(Guid identificador, int caracteresPorLinea, bool esReimpresion)
    {
        _sesion.ExigirUsuario();

        if (_baseLocal is not null && await _baseLocal.LeerVentaPendienteAsync(identificador) is { } pendiente)
        {
            var lineas = TicketVenta.Armar(pendiente, _comercio, _datosFiscales, caracteresPorLinea, esReimpresion);
            return new TicketParaImprimir(MarcarDemostracion(lineas, caracteresPorLinea), null);
        }

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var ventaId = await contexto.Ventas
            .Where(v => v.IdentificadorUnico == identificador)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync()
            ?? throw new VentaInvalidaException("La venta no existe.");

        return await ArmarAsync(ventaId, caracteresPorLinea, esReimpresion);
    }

    /// <summary>
    /// Lee la venta guardada y devuelve las líneas del ticket (y el QR si es una factura autorizada).
    /// Se arma siempre desde la base (no desde el carrito), así la reimpresión sale igual que el original
    /// (o como factura, si el CAE llegó después).
    /// </summary>
    public async Task<TicketParaImprimir> ArmarAsync(int ventaId, int caracteresPorLinea, bool esReimpresion)
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var venta = await contexto.Ventas
            .AsNoTracking()
            .Include(v => v.Detalles.OrderBy(d => d.Id))
            .Include(v => v.MedioPago)
            .Include(v => v.Usuario)
            .Include(v => v.TurnoCaja!.Caja)
            .Include(v => v.Factura!.Alicuotas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == ventaId)
            ?? throw new VentaInvalidaException($"La venta N° {ventaId} no existe.");

        var lineas = TicketVenta.Armar(venta, _comercio, _datosFiscales, caracteresPorLinea, esReimpresion);
        var qr = venta.Factura is null ? null : _facturacion.ArmarQr(venta.Factura);
        return new TicketParaImprimir(MarcarDemostracion(lineas, caracteresPorLinea), qr);
    }

    // Comprobante de cierre de caja (arqueo) de un turno, armado desde la base
    public async Task<IReadOnlyList<string>> ArmarCierreAsync(int turnoId, int caracteresPorLinea)
    {
        var resumen = await _cajaServicio.ObtenerResumenAsync(turnoId);
        return MarcarDemostracion(TicketCierreCaja.Armar(resumen, _comercio, caracteresPorLinea), caracteresPorLinea);
    }

    // Sin licencia válida, todos los comprobantes salen marcados
    private IReadOnlyList<string> MarcarDemostracion(IReadOnlyList<string> lineas, int ancho)
    {
        if (_licencia?.EsValida != false)
            return lineas;

        var marcadas = lineas.ToList();
        FormatoTicket.Centrar(marcadas, "*** MODO DEMOSTRACIÓN - SIN LICENCIA ***", ancho);
        return marcadas;
    }
}
