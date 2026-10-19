namespace MiPOS.Negocio.Facturacion;

/// <summary>
/// Se rompió una regla de facturación (datos del cliente inválidos, configuración incompleta,
/// factura rechazada por ARCA...). Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class FacturaInvalidaException : Exception
{
    public FacturaInvalidaException(string mensaje) : base(mensaje) { }
}
