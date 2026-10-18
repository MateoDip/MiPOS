using MiPOS.Dominio.Entidades;
using static MiPOS.Negocio.Comprobantes.FormatoTicket;

namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Arma el texto del ticket (no fiscal) de una venta, línea por línea,
/// con el ancho exacto del papel. No sabe nada de impresoras: solo devuelve las líneas.
/// </summary>
public static class TicketVenta
{
    // La venta tiene que venir con Detalles, MedioPago, Usuario y TurnoCaja.Caja cargados
    public static IReadOnlyList<string> Armar(Venta venta, DatosComercio comercio, int ancho, bool esReimpresion)
    {
        var lineas = new List<string>();
        var separador = new string('-', ancho);

        EncabezadoComercio(lineas, comercio, ancho);

        if (esReimpresion)
            Centrar(lineas, "*** REIMPRESIÓN ***", ancho);

        lineas.Add(separador);
        DosColumnas(lineas, $"Venta N° {venta.Id}", venta.Fecha.ToString("dd/MM/yyyy HH:mm", Argentina), ancho);
        if (venta.TurnoCaja?.Caja is { } caja)
            AjustarTexto(lineas, caja.Nombre, ancho);
        AjustarTexto(lineas, $"Cajero: {venta.Usuario?.NombreCompleto}", ancho);
        lineas.Add(separador);

        // Productos: "2 x $ 2.500,00" y abajo el nombre con el subtotal a la derecha
        foreach (var detalle in venta.Detalles)
        {
            AjustarTexto(lineas, $"{detalle.Cantidad} x {Dinero(detalle.PrecioUnitario)}", ancho);
            DosColumnas(lineas, detalle.NombreProducto, Dinero(detalle.Subtotal), ancho);
        }

        lineas.Add(separador);

        if (venta.Descuento > 0)
        {
            DosColumnas(lineas, "Subtotal", Dinero(venta.Subtotal), ancho);
            DosColumnas(lineas, $"Descuento {venta.DescuentoPorcentaje.ToString("0.##", Argentina)}%", $"-{Dinero(venta.Descuento)}", ancho);
        }

        DosColumnas(lineas, "TOTAL", Dinero(venta.Total), ancho);

        // Medio de pago: en efectivo, con cuánto pagó y el vuelto
        var medio = venta.MedioPago?.Nombre ?? string.Empty;
        DosColumnas(lineas, medio, Dinero(venta.MontoRecibido ?? venta.Total), ancho);
        if (venta.Vuelto is decimal vuelto)
            DosColumnas(lineas, "Vuelto", Dinero(vuelto), ancho);

        lineas.Add(separador);

        // Obligatorio mientras el ticket no sea fiscal (la factura con ARCA llega en la Fase 6)
        Centrar(lineas, "NO VÁLIDO COMO FACTURA", ancho);
        Centrar(lineas, comercio.MensajePie, ancho);

        return lineas;
    }
}
