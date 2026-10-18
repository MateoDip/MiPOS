using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Negocio.Ventas;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

public class TicketServicio
{
    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly DatosComercio _comercio;
    private readonly Sesion _sesion;

    public TicketServicio(IDbContextFactory<MiPOSDbContext> fabrica, DatosComercio comercio, Sesion sesion)
    {
        _fabrica = fabrica;
        _comercio = comercio;
        _sesion = sesion;
    }

    /// <summary>
    /// Lee la venta guardada y devuelve las líneas del ticket.
    /// Se arma siempre desde la base (no desde el carrito), así la reimpresión sale igual que el original.
    /// </summary>
    public async Task<IReadOnlyList<string>> ArmarAsync(int ventaId, int caracteresPorLinea, bool esReimpresion)
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var venta = await contexto.Ventas
            .AsNoTracking()
            .Include(v => v.Detalles.OrderBy(d => d.Id))
            .Include(v => v.MedioPago)
            .Include(v => v.Usuario)
            .FirstOrDefaultAsync(v => v.Id == ventaId)
            ?? throw new VentaInvalidaException($"La venta N° {ventaId} no existe.");

        return TicketVenta.Armar(venta, _comercio, caracteresPorLinea, esReimpresion);
    }
}
