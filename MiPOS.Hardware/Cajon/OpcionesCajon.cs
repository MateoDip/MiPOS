namespace MiPOS.Hardware.Cajon;

public enum ModoCajon
{
    // No hay cajón (o lo abre solo el driver de la impresora: ver LEEME)
    Desactivado,

    // El cajón está enchufado a la impresora de tickets (cable RJ11): se le manda el pulso a la impresora
    Impresora,

    // El cajón tiene su propio cable USB o serie (aparece como un puerto COM)
    Serial
}

/// <summary>
/// Configuración del cajón de dinero. Se lee de la sección "Cajon" de appsettings.json (cada caja tiene el suyo).
/// </summary>
public class OpcionesCajon
{
    public ModoCajon Modo { get; set; } = ModoCajon.Desactivado;

    // Solo Modo = Impresora. Vacío = la misma impresora de los tickets (Impresion:Impresora).
    public string Impresora { get; set; } = string.Empty;

    // Solo Modo = Serial
    public string Puerto { get; set; } = "COM1";
    public int Baudios { get; set; } = 9600;

    // Bytes del pulso de apertura, en hexadecimal. El valor por defecto (ESC p 0 25 250)
    // es el estándar de casi todas las impresoras térmicas. Algunos cajones usan el pin 5: "1B 70 01 19 FA".
    public string Comando { get; set; } = "1B 70 00 19 FA";

    public bool EstaActivo => Modo != ModoCajon.Desactivado;

    // Convierte el texto "1B 70 00 19 FA" en los bytes a mandar
    public byte[] LeerComando()
    {
        try
        {
            var partes = Comando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0)
                throw new FormatException();

            return partes.Select(p => Convert.ToByte(p, 16)).ToArray();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            throw new HardwareException($"Configuración del cajón inválida: Cajon:Comando debe tener bytes en hexadecimal separados por espacios (ej: \"1B 70 00 19 FA\"), no \"{Comando}\".", ex);
        }
    }
}
