using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Ventas;

namespace MiPOS.Negocio.Servicios;

public class VentaServicio
{
    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;

    public VentaServicio(IDbContextFactory<MiPOSDbContext> fabrica)
    {
        _fabrica = fabrica;
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

    /// <summary>
    /// Valida y guarda la venta con todos sus detalles.
    /// montoRecibido solo se usa en efectivo (en los demás medios se ignora).
    /// </summary>
    public async Task<Venta> RegistrarAsync(Carrito carrito, int medioPagoId, decimal? montoRecibido)
    {
        if (carrito.EstaVacio)
            throw new VentaInvalidaException("No hay productos para cobrar.");

        var total = carrito.Total;

        // Se arma la venta antes de ir a la base, con lo que hay en el carrito en este momento
        var venta = new Venta
        {
            Fecha = DateTime.Now,
            Total = total,
            MedioPagoId = medioPagoId,
            Detalles = carrito.Items.Select(i => new DetalleVenta
            {
                ProductoId = i.ProductoId,
                NombreProducto = i.Nombre,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario,
                Subtotal = i.Subtotal
            }).ToList()
        };

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var medio = await contexto.MediosPago
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == medioPagoId && m.Activo);

        if (medio is null)
            throw new VentaInvalidaException("El medio de pago no existe o está desactivado.");

        if (medio.EsEfectivo)
        {
            if (montoRecibido is null)
                throw new VentaInvalidaException("Ingrese con cuánto paga el cliente.");

            venta.MontoRecibido = montoRecibido;
            venta.Vuelto = Cobro.CalcularVuelto(total, montoRecibido.Value);
        }

        // Un solo SaveChanges: se guarda la venta completa con sus detalles, o no se guarda nada
        contexto.Ventas.Add(venta);
        await contexto.SaveChangesAsync();

        return venta;
    }
}
