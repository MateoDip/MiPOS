namespace MiPOS.Negocio.Ventas;

/// <summary>
/// Error de una regla de la venta (por ejemplo, el efectivo no alcanza).
/// Su mensaje se puede mostrar tal cual al cajero.
/// Se separa de los errores de base de datos, que se muestran con otro mensaje.
/// </summary>
public class VentaInvalidaException : Exception
{
    public VentaInvalidaException(string mensaje) : base(mensaje) { }
}
