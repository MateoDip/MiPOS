namespace MiPOS.Hardware;

/// <summary>
/// Falló un dispositivo (cajón, lector, terminal de pago): desconectado, mal configurado, etc.
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class HardwareException : Exception
{
    public HardwareException(string mensaje, Exception? causa = null) : base(mensaje, causa) { }
}
