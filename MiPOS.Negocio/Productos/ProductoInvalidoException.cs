namespace MiPOS.Negocio.Productos;

/// <summary>
/// Se rompió una regla de productos, categorías o stock (por ejemplo, un código de barras repetido).
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class ProductoInvalidoException : Exception
{
    public ProductoInvalidoException(string mensaje) : base(mensaje) { }
}
