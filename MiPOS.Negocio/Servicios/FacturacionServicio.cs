using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Facturacion;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Negocio.Comun;
using MiPOS.Negocio.Facturacion;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

// Cuántas facturas pendientes se autorizaron y cuántas siguen pendientes
public record ResultadoPendientes(int Autorizadas, int SiguenPendientes, int Rechazadas, string? UltimoError);

/// <summary>
/// Factura electrónica de las ventas. La factura se guarda Pendiente junto con la venta
/// (PrepararFactura, desde VentaServicio) y después se le pide el CAE a ARCA (AutorizarAsync).
/// Si no hay conexión, queda pendiente y se reintenta más tarde: la venta nunca se pierde.
/// </summary>
public class FacturacionServicio
{
    // ARCA rechaza con este código un número que no es el siguiente al último autorizado
    private const int ErrorNumeroNoCorrelativo = 10016;

    // Las facturas de esta PC se autorizan de a una (la numeración tiene que ser correlativa)
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;
    private readonly OpcionesFacturacion _opciones;
    private readonly DatosFiscales _datosFiscales;
    private readonly DatosComercio _comercio;
    private readonly IServicioArca _arca;

    public FacturacionServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion, OpcionesFacturacion opciones,
                               DatosFiscales datosFiscales, DatosComercio comercio, IServicioArca arca)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _opciones = opciones;
        _datosFiscales = datosFiscales;
        _comercio = comercio;
        _arca = arca;
    }

    public bool EstaActiva => _opciones.EstaActiva;

    // Qué factura le corresponde a este cliente (según la condición frente al IVA del comercio)
    public TipoComprobante TipoPara(DatosReceptor receptor)
        => CalculadoraFactura.DeterminarTipo(_datosFiscales.CondicionIva, receptor.CondicionIva);

    // Ej: "Factura B · Consumidor final" (para mostrar en el cobro)
    public string Describir(DatosReceptor receptor)
    {
        var tipo = CalculadoraFactura.NombreTipo(TipoPara(receptor));
        var nombre = receptor.Nombre is null ? string.Empty : $" · {receptor.Nombre}";
        return $"{tipo[0]}{tipo[1..].ToLowerInvariant()} · {receptor.DocumentoTexto}{nombre}";
    }

    /// <summary>
    /// Arma la factura pendiente de una venta que todavía no se guardó (la guarda VentaServicio,
    /// en la misma transacción que la venta). null si la facturación está desactivada.
    /// </summary>
    internal Factura? PrepararFactura(Venta venta, DatosReceptor? receptor)
    {
        if (!_opciones.EstaActiva)
            return null;

        _opciones.Validar();
        CuitEmisor(); // si la configuración está mal, mejor saberlo antes de cobrar

        receptor ??= DatosReceptor.ConsumidorFinal;

        if (!receptor.EstaIdentificado && venta.Total >= _datosFiscales.MontoMaximoSinIdentificar)
            throw new FacturaInvalidaException(
                $"Para ventas desde $ {_datosFiscales.MontoMaximoSinIdentificar:N2} ARCA exige identificar al cliente. Ingrese su DNI o CUIT (F2 en el cobro).");

        var tipo = TipoPara(receptor);
        var importes = CalculadoraFactura.Calcular(tipo,
            venta.Detalles.Select(d => (d.AlicuotaIva, d.Subtotal)), venta.Descuento);

        if (importes.Total != venta.Total)
            throw new FacturaInvalidaException("Error interno: el total de la factura no coincide con el de la venta.");

        return new Factura
        {
            Tipo = tipo,
            PuntoVenta = _opciones.PuntoVenta,
            Estado = EstadoFactura.Pendiente,
            DocTipo = receptor.DocTipo,
            DocNumero = receptor.DocNumero,
            ReceptorNombre = receptor.Nombre,
            ReceptorCondicionIva = receptor.CondicionIva,
            ImporteNeto = importes.Neto,
            ImporteExento = importes.Exento,
            ImporteIva = importes.Iva,
            Total = importes.Total,
            Alicuotas = importes.Alicuotas.ToList(),
            Entorno = _opciones.Modo.ToString(),
            FechaCreacion = DateTime.Now
        };
    }

    // Pide el CAE de la factura de una venta. null si la venta no tiene factura.
    public async Task<Factura?> AutorizarVentaAsync(int ventaId, CancellationToken cancelacion = default)
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var facturaId = await contexto.Facturas
            .Where(f => f.VentaId == ventaId)
            .Select(f => (int?)f.Id)
            .FirstOrDefaultAsync(cancelacion);

        return facturaId is null ? null : await AutorizarAsync(facturaId.Value, cancelacion);
    }

    /// <summary>
    /// Pide el CAE a ARCA. Devuelve la factura autorizada.
    /// Lanza FacturacionException si no hubo conexión (la factura sigue pendiente)
    /// o FacturaInvalidaException si ARCA la rechazó.
    /// </summary>
    public async Task<Factura> AutorizarAsync(int facturaId, CancellationToken cancelacion = default)
    {
        _sesion.ExigirUsuario();

        await Candado.WaitAsync(cancelacion);
        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync();

            var factura = await contexto.Facturas
                .Include(f => f.Alicuotas)
                .FirstOrDefaultAsync(f => f.Id == facturaId, cancelacion)
                ?? throw new FacturaInvalidaException("La factura no existe.");

            if (factura.Estado == EstadoFactura.Autorizada)
                return factura;

            if (factura.Estado == EstadoFactura.Rechazada)
                throw new FacturaInvalidaException($"ARCA rechazó esta factura: {factura.Mensaje}");

            if (factura.Entorno != _opciones.Modo.ToString())
                throw new FacturaInvalidaException(
                    $"La factura se emitió con la facturación en modo {factura.Entorno} y ahora está en modo {_opciones.Modo}. No se puede autorizar.");

            if (factura.PuntoVenta != _opciones.PuntoVenta)
                throw new FacturaInvalidaException(
                    $"La factura es del punto de venta {factura.PuntoVenta}: hay que autorizarla desde la caja que usa ese punto de venta.");

            var cuit = CuitEmisor();

            try
            {
                var ultimo = await _arca.ObtenerUltimoNumeroAsync(cuit, factura.PuntoVenta, (int)factura.Tipo, cancelacion);

                // Ya se había pedido el CAE con este número: puede que ARCA lo haya autorizado
                // y que la respuesta no haya llegado. Se consulta antes de pedir otro número.
                if (factura.Numero > 0 && factura.Numero <= ultimo)
                {
                    var consultado = await _arca.ConsultarAsync(cuit, factura.PuntoVenta, (int)factura.Tipo, factura.Numero, cancelacion);

                    if (consultado is not null && consultado.ImporteTotal == factura.Total && consultado.DocNumero == factura.DocNumero)
                    {
                        MarcarAutorizada(factura, consultado.Cae, consultado.VencimientoCae, consultado.Fecha, "Autorizada al reintentar (la respuesta anterior de ARCA no había llegado).");
                        await contexto.SaveChangesAsync(CancellationToken.None);
                        return factura;
                    }
                }

                // El número se guarda ANTES de pedir el CAE (ver arriba)
                factura.Numero = ultimo + 1;
                factura.Fecha = DateOnly.FromDateTime(DateTime.Now);
                factura.Intentos++;
                await contexto.SaveChangesAsync(CancellationToken.None);

                var respuesta = await _arca.SolicitarCaeAsync(cuit, ArmarSolicitud(factura), cancelacion);

                if (respuesta.Aprobado)
                {
                    MarcarAutorizada(factura, respuesta.Cae!, respuesta.VencimientoCae!.Value, factura.Fecha.Value,
                        string.IsNullOrEmpty(respuesta.Mensajes) ? null : respuesta.Mensajes);
                    await contexto.SaveChangesAsync(CancellationToken.None);
                    return factura;
                }

                if (respuesta.Codigos.Contains(ErrorNumeroNoCorrelativo))
                {
                    // Otra PC usó el mismo punto de venta (no debería). Sigue pendiente: al reintentar se toma el siguiente.
                    factura.Mensaje = Recortar($"ARCA no aceptó el número de factura: {respuesta.Mensajes}");
                    await contexto.SaveChangesAsync(CancellationToken.None);
                    throw new FacturacionException(
                        $"ARCA no aceptó el número de factura. ¿Hay otra caja usando el punto de venta {factura.PuntoVenta}? Reintente.",
                        reintentable: true);
                }

                factura.Estado = EstadoFactura.Rechazada;
                factura.Numero = 0; // ese número no quedó usado en ARCA
                factura.Mensaje = Recortar(respuesta.Mensajes);
                await contexto.SaveChangesAsync(CancellationToken.None);
                throw new FacturaInvalidaException($"ARCA rechazó la factura: {respuesta.Mensajes}");
            }
            catch (FacturacionException ex)
            {
                // Sin conexión o error técnico: la factura sigue pendiente con el número ya guardado
                factura.Mensaje = Recortar(ex.Message);
                await contexto.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            Candado.Release();
        }
    }

    /// <summary>
    /// Pide el CAE de todas las facturas pendientes de esta caja, de la más vieja a la más nueva.
    /// Si se corta la conexión, deja de intentar (las demás seguirían fallando).
    /// </summary>
    public async Task<ResultadoPendientes> AutorizarPendientesAsync(CancellationToken cancelacion = default)
    {
        _sesion.ExigirUsuario();

        if (!_opciones.EstaActiva)
            return new ResultadoPendientes(0, 0, 0, null);

        List<int> ids;
        await using (var contexto = await _fabrica.CreateDbContextAsync())
        {
            ids = await contexto.Facturas
                .Where(f => f.Estado == EstadoFactura.Pendiente
                            && f.PuntoVenta == _opciones.PuntoVenta
                            && f.Entorno == _opciones.Modo.ToString())
                .OrderBy(f => f.Id)
                .Select(f => f.Id)
                .ToListAsync(cancelacion);
        }

        int autorizadas = 0, rechazadas = 0;
        string? ultimoError = null;

        foreach (var id in ids)
        {
            try
            {
                await AutorizarAsync(id, cancelacion);
                autorizadas++;
            }
            catch (FacturaInvalidaException ex)
            {
                rechazadas++;
                ultimoError = ex.Message;
            }
            catch (FacturacionException ex)
            {
                ultimoError = ex.Message;
                break;
            }
        }

        return new ResultadoPendientes(autorizadas, ids.Count - autorizadas - rechazadas, rechazadas, ultimoError);
    }

    // Pendientes y rechazadas (de todas las cajas), las más viejas primero
    public async Task<List<Factura>> ObtenerSinAutorizarAsync()
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Facturas
            .AsNoTracking()
            .Include(f => f.Venta)
            .Where(f => f.Estado != EstadoFactura.Autorizada)
            .OrderBy(f => f.Id)
            .ToListAsync();
    }

    public async Task<int> ContarSinAutorizarAsync()
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();
        return await contexto.Facturas.CountAsync(f => f.Estado != EstadoFactura.Autorizada);
    }

    // Una factura rechazada vuelve a pendiente (por ejemplo, después de corregir la configuración en ARCA)
    public async Task VolverAPendienteAsync(int facturaId)
    {
        _sesion.Exigir(Permiso.ConfigurarFacturacion);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var filas = await contexto.Facturas
            .Where(f => f.Id == facturaId && f.Estado == EstadoFactura.Rechazada)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Estado, EstadoFactura.Pendiente));

        if (filas == 0)
            throw new FacturaInvalidaException("La factura no existe o no está rechazada.");
    }

    // Texto del código QR de una factura autorizada (null si no está autorizada)
    public string? ArmarQr(Factura factura)
    {
        if (factura.Estado != EstadoFactura.Autorizada || factura.Cae is null || factura.Fecha is null)
            return null;

        return CodigoQrArca.ArmarUrl(factura.Fecha.Value, CuitEmisor(), factura.PuntoVenta, (int)factura.Tipo,
            factura.Numero, factura.Total, factura.DocTipo, factura.DocNumero, factura.Cae);
    }

    // ----- Ayudas -----

    private long CuitEmisor()
    {
        if (!Cuit.IntentarNormalizar(_comercio.Cuit, out var cuit))
            throw new FacturaInvalidaException("El CUIT del comercio (Comercio:Cuit en appsettings.json) es inválido: no se puede facturar.");

        return long.Parse(cuit);
    }

    private static SolicitudCae ArmarSolicitud(Factura factura) => new()
    {
        PuntoVenta = factura.PuntoVenta,
        TipoComprobante = (int)factura.Tipo,
        Numero = factura.Numero,
        Fecha = factura.Fecha!.Value,
        DocTipo = factura.DocTipo,
        DocNumero = factura.DocNumero,
        CondicionIvaReceptor = (int)factura.ReceptorCondicionIva,
        ImporteTotal = factura.Total,
        ImporteNeto = factura.ImporteNeto,
        ImporteExento = factura.ImporteExento,
        ImporteIva = factura.ImporteIva,
        Alicuotas = factura.Alicuotas
            .Select(a => new AlicuotaArca((int)a.Alicuota, a.BaseImponible, a.Importe))
            .ToList()
    };

    private static void MarcarAutorizada(Factura factura, string cae, DateOnly vencimiento, DateOnly fecha, string? mensaje)
    {
        factura.Estado = EstadoFactura.Autorizada;
        factura.Cae = cae;
        factura.VencimientoCae = vencimiento;
        factura.Fecha = fecha;
        factura.Mensaje = Recortar(mensaje);
    }

    private static string? Recortar(string? texto)
        => texto is { Length: > 1000 } ? texto[..1000] : texto;
}
