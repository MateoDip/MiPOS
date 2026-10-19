namespace MiPOS.Negocio.Compras;

/// <summary>
/// Se rompió una regla de compras o proveedores (por ejemplo, una compra sin productos).
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class CompraInvalidaException : Exception
{
    public CompraInvalidaException(string mensaje) : base(mensaje) { }
}
