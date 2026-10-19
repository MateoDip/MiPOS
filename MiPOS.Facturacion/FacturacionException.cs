namespace MiPOS.Facturacion;

/// <summary>
/// No se pudo hablar con ARCA, o ARCA devolvió un error técnico.
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class FacturacionException : Exception
{
    public FacturacionException(string mensaje, bool reintentable = false, Exception? causa = null)
        : base(mensaje, causa)
    {
        Reintentable = reintentable;
    }

    // true = problema pasajero (sin Internet, ARCA caído, demora): se puede volver a intentar más tarde
    public bool Reintentable { get; }
}
