namespace MiPOS.Negocio.Cajas;

/// <summary>
/// Error de una regla del control de caja (por ejemplo, la caja ya está abierta
/// o el retiro supera el efectivo). Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class CajaInvalidaException : Exception
{
    public CajaInvalidaException(string mensaje) : base(mensaje) { }
}
