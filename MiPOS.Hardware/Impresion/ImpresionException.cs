namespace MiPOS.Hardware.Impresion;

/// <summary>
/// No se pudo imprimir (impresora no encontrada, desconectada, sin papel, configuración inválida...).
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class ImpresionException : Exception
{
    public ImpresionException(string mensaje, Exception? causa = null) : base(mensaje, causa) { }
}
