using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.DatosLocal;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.SinConexion;
using MiPOS.Negocio.Ventas;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

public class VentaServicio
{
    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;
    private readonly OpcionesCaja _opcionesCaja;
    private readonly FacturacionServicio _facturacion;
    private readonly BaseLocal? _baseLocal;

    public VentaServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion, OpcionesCaja opcionesCaja,
                         FacturacionServicio facturacion, BaseLocal? baseLocal = null)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _opcionesCaja = opcionesCaja;
        _facturacion = facturacion;
        _baseLocal = baseLocal;
    }

    public async Task<List<MedioPago>> ObtenerMediosPagoAsync()
    {
        if (_baseLocal?.UsarLocal == true)
            return await ObtenerMediosPagoLocalesAsync();

        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync();

            return await contexto.MediosPago
                .AsNoTracking()
                .Where(m => m.Activo)
                .OrderBy(m => m.Id)
                .ToListAsync();
        }
        catch (Exception ex) when (_baseLocal?.EsCorte(ex) == true)
        {
            return await ObtenerMediosPagoLocalesAsync();
        }
    }

    private async Task<List<MedioPago>> ObtenerMediosPagoLocalesAsync()
    {
        await using var local = await _baseLocal!.CrearContextoAsync();
        return await local.MediosPago.AsNoTracking().Where(m => m.Activo).OrderBy(m => m.Id).ToListAsync();
    }

    // La última venta del usuario conectado (para reimprimir su ticket). null si no tiene ventas.
    public async Task<int?> ObtenerUltimaVentaIdAsync()
    {
        var usuario = _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Ventas
            .Where(v => v.UsuarioId == usuario.Id)
            .MaxAsync(v => (int?)v.Id);
    }

    /// <summary>
    /// Valida y guarda la venta con todos sus detalles.
    /// montoRecibido solo se usa en efectivo (en los demás medios se ignora).
    /// receptor: a quién se factura (null = consumidor final). Si la facturación está activa,
    /// la factura queda guardada como pendiente; el CAE se pide después (FacturacionServicio).
    /// referenciaPago: cupón u operación de la terminal (solo en medios con terminal; opcional).
    /// Sin conexión con la base central, la venta se guarda en la base local de la caja
    /// (RegistradaSinConexion = true) y SincronizacionServicio la sube cuando vuelve la conexión.
    /// </summary>
    public async Task<Venta> RegistrarAsync(Carrito carrito, int medioPagoId, decimal? montoRecibido,
                                            DatosReceptor? receptor = null, string? referenciaPago = null)
    {
        // Toda venta queda registrada a nombre del usuario conectado
        var usuario = _sesion.UsuarioActual
            ?? throw new VentaInvalidaException("No hay un usuario con sesión iniciada.");

        if (carrito.EstaVacio)
            throw new VentaInvalidaException("No hay productos para cobrar.");

        // Se arma la venta antes de ir a la base, con lo que hay en el carrito en este momento
        var venta = new Venta
        {
            // El identificador nace acá: si la venta termina guardada dos veces (en la central y en la local),
            // al subirla se reconoce y no se duplica
            IdentificadorUnico = Guid.NewGuid(),
            Fecha = DateTime.Now,
            Subtotal = carrito.Subtotal,
            DescuentoPorcentaje = carrito.DescuentoPorcentaje,
            Descuento = carrito.Descuento,
            DescuentoAutorizadoPorId = carrito.DescuentoAutorizadoPorId,
            Total = carrito.Total,
            UsuarioId = usuario.Id,
            MedioPagoId = medioPagoId,
            Detalles = carrito.Items.Select(i => new DetalleVenta
            {
                ProductoId = i.ProductoId,
                NombreProducto = i.Nombre,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario,
                Subtotal = i.Subtotal,
                AlicuotaIva = i.AlicuotaIva
            }).ToList()
        };

        // La factura (si la facturación está activa) se guarda con la venta, como pendiente
        venta.Factura = _facturacion.PrepararFactura(venta, receptor);

        if (_baseLocal?.UsarLocal == true)
            return await RegistrarSinConexionAsync(venta, carrito, usuario, montoRecibido, referenciaPago);

        try
        {
            return await RegistrarEnBaseCentralAsync(venta, carrito, usuario, montoRecibido, referenciaPago);
        }
        catch (Exception ex) when (_baseLocal?.EsCorte(ex) == true)
        {
            // Se cortó la conexión en medio del registro: la base central deshizo todo (transacción).
            // Si justo llegó a confirmarse, al subirla se reconoce por el IdentificadorUnico.
            LimpiarIds(venta);
            return await RegistrarSinConexionAsync(venta, carrito, usuario, montoRecibido, referenciaPago);
        }
    }

    private async Task<Venta> RegistrarEnBaseCentralAsync(Venta venta, Carrito carrito, Usuario usuario,
                                                          decimal? montoRecibido, string? referenciaPago)
    {
        await using var contexto = await _fabrica.CreateDbContextAsync();

        // Toda venta se cobra dentro de un turno abierto de la caja de esta PC
        var turnoId = await contexto.TurnosCaja
            .Where(t => t.Caja!.Numero == _opcionesCaja.Numero && t.FechaCierre == null)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync()
            ?? throw new VentaInvalidaException("La caja está cerrada. Ábrala con F11 antes de cobrar.");

        venta.TurnoCajaId = turnoId;

        var medio = await contexto.MediosPago
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == venta.MedioPagoId && m.Activo);

        // Si hay descuento, se vuelve a comprobar en la base que quien lo autorizó
        // existe, está activo y tiene el permiso (no alcanza con lo que dice la pantalla)
        var autorizante = carrito.TieneDescuento
            ? await contexto.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == carrito.DescuentoAutorizadoPorId && u.Activo)
            : null;

        Validar(venta, carrito, medio, autorizante, montoRecibido, referenciaPago);

        // Todo en una transacción: la venta con sus detalles y el descuento de stock
        // se guardan juntos, o no se guarda nada
        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        contexto.Ventas.Add(venta);
        await contexto.SaveChangesAsync(); // acá la venta recibe su Id

        // Se descuenta aunque no alcance el stock: si el cliente tiene el producto
        // en la mano, existe. El stock queda negativo y aparece en las alertas.
        foreach (var detalle in venta.Detalles)
            await StockServicio.RegistrarMovimientoAsync(contexto, detalle.ProductoId, TipoMovimientoStock.Venta,
                -detalle.Cantidad, motivo: null, usuario.Id, venta.Id);

        await contexto.SaveChangesAsync();
        await transaccion.CommitAsync();

        return venta;
    }

    // Las mismas reglas que en la base central, con la copia local (medios de pago, usuarios y turno).
    // El stock no se descuenta acá: se descuenta en la base central al subir la venta.
    private async Task<Venta> RegistrarSinConexionAsync(Venta venta, Carrito carrito, Usuario usuario,
                                                        decimal? montoRecibido, string? referenciaPago)
    {
        await using var local = await _baseLocal!.CrearContextoAsync();

        var turno = await local.LeerAsync<TurnoCaja>(BaseLocal.ClaveTurnoAbierto)
            ?? throw new VentaInvalidaException(
                "No hay conexión con la base central y la caja figuraba cerrada: no se puede vender hasta que vuelva la conexión.");

        venta.TurnoCajaId = turno.Id;

        var medio = await local.MediosPago.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == venta.MedioPagoId && m.Activo);

        var autorizante = carrito.TieneDescuento
            ? await local.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == carrito.DescuentoAutorizadoPorId && u.Activo)
            : null;

        Validar(venta, carrito, medio, autorizante, montoRecibido, referenciaPago);

        var pendiente = new VentaPendiente
        {
            IdentificadorUnico = venta.IdentificadorUnico!.Value,
            Fecha = venta.Fecha,
            Datos = JsonSerializer.Serialize(venta, ValorLocal.OpcionesJson)
        };

        local.VentasPendientes.Add(pendiente);
        await local.SaveChangesAsync();

        // Para el ticket: el número local y los datos que la venta guardada no trae
        venta.Id = pendiente.Id;
        venta.RegistradaSinConexion = true;
        venta.MedioPago = medio;
        venta.Usuario = usuario;
        venta.TurnoCaja = turno;
        turno.Caja = await local.LeerAsync<Caja>(BaseLocal.ClaveCaja);
        return venta;
    }

    // Reglas del medio de pago, del descuento y del pago en efectivo. Completa la venta.
    private static void Validar(Venta venta, Carrito carrito, MedioPago? medio, Usuario? autorizante,
                                decimal? montoRecibido, string? referenciaPago)
    {
        if (medio is null)
            throw new VentaInvalidaException("El medio de pago no existe o está desactivado.");

        if (carrito.TieneDescuento && (autorizante is null || !Permisos.Tiene(autorizante.Rol, Permiso.AplicarDescuento)))
            throw new VentaInvalidaException("El descuento no está autorizado por un usuario con permiso. Quítelo o vuelva a aplicarlo.");

        if (medio.UsaTerminal && !string.IsNullOrWhiteSpace(referenciaPago))
        {
            referenciaPago = referenciaPago.Trim();
            if (referenciaPago.Length > 100)
                throw new VentaInvalidaException("El número de cupón u operación no puede tener más de 100 caracteres.");

            venta.ReferenciaPago = referenciaPago;
        }

        if (medio.EsEfectivo)
        {
            if (montoRecibido is null)
                throw new VentaInvalidaException("Ingrese con cuánto paga el cliente.");

            venta.MontoRecibido = montoRecibido;
            venta.Vuelto = Cobro.CalcularVuelto(venta.Total, montoRecibido.Value);
        }
    }

    // Deja la venta como recién armada (sin los Id que le puso una base), para guardarla en otra
    internal static void LimpiarIds(Venta venta)
    {
        venta.Id = 0;
        foreach (var detalle in venta.Detalles)
        {
            detalle.Id = 0;
            detalle.VentaId = 0;
            detalle.Venta = null;
        }

        if (venta.Factura is { } factura)
        {
            factura.Id = 0;
            factura.VentaId = 0;
            factura.Venta = null;
            foreach (var alicuota in factura.Alicuotas)
            {
                alicuota.Id = 0;
                alicuota.FacturaId = 0;
                alicuota.Factura = null;
            }
        }
    }
}
