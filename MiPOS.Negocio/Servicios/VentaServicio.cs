using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Ventas;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

public class VentaServicio
{
    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;
    private readonly OpcionesCaja _opcionesCaja;
    private readonly FacturacionServicio _facturacion;

    public VentaServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion, OpcionesCaja opcionesCaja,
                         FacturacionServicio facturacion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _opcionesCaja = opcionesCaja;
        _facturacion = facturacion;
    }

    public async Task<List<MedioPago>> ObtenerMediosPagoAsync()
    {
        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.MediosPago
            .AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Id)
            .ToListAsync();
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
    /// </summary>
    public async Task<Venta> RegistrarAsync(Carrito carrito, int medioPagoId, decimal? montoRecibido,
                                            DatosReceptor? receptor = null, string? referenciaPago = null)
    {
        // Toda venta queda registrada a nombre del usuario conectado
        var usuario = _sesion.UsuarioActual
            ?? throw new VentaInvalidaException("No hay un usuario con sesión iniciada.");

        if (carrito.EstaVacio)
            throw new VentaInvalidaException("No hay productos para cobrar.");

        var total = carrito.Total;

        // Se arma la venta antes de ir a la base, con lo que hay en el carrito en este momento
        var venta = new Venta
        {
            Fecha = DateTime.Now,
            Subtotal = carrito.Subtotal,
            DescuentoPorcentaje = carrito.DescuentoPorcentaje,
            Descuento = carrito.Descuento,
            DescuentoAutorizadoPorId = carrito.DescuentoAutorizadoPorId,
            Total = total,
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
            .FirstOrDefaultAsync(m => m.Id == medioPagoId && m.Activo);

        if (medio is null)
            throw new VentaInvalidaException("El medio de pago no existe o está desactivado.");

        // Si hay descuento, se vuelve a comprobar en la base que quien lo autorizó
        // existe, está activo y tiene el permiso (no alcanza con lo que dice la pantalla)
        if (carrito.TieneDescuento)
        {
            var autorizante = await contexto.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == carrito.DescuentoAutorizadoPorId && u.Activo);

            if (autorizante is null || !Permisos.Tiene(autorizante.Rol, Permiso.AplicarDescuento))
                throw new VentaInvalidaException("El descuento no está autorizado por un usuario con permiso. Quítelo o vuelva a aplicarlo.");
        }

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
            venta.Vuelto = Cobro.CalcularVuelto(total, montoRecibido.Value);
        }

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
}
