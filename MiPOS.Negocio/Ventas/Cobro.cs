namespace MiPOS.Negocio.Ventas;

/// <summary>
/// Reglas del cobro que no necesitan base de datos.
/// Las usa la pantalla (para mostrar el vuelto mientras se escribe) y VentaServicio (para validar).
/// </summary>
public static class Cobro
{
    public static bool AlcanzaElPago(decimal total, decimal montoRecibido) => montoRecibido >= total;

    public static decimal CalcularVuelto(decimal total, decimal montoRecibido)
    {
        if (!AlcanzaElPago(total, montoRecibido))
            throw new VentaInvalidaException("El monto recibido no alcanza para pagar el total.");

        return montoRecibido - total;
    }
}
