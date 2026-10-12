using System.Globalization;

namespace MiPOS.WPF.Servicios;

/// <summary>
/// Lee números escritos por el cajero (montos, porcentajes).
/// </summary>
public static class LectorNumeros
{
    // Acepta solo números con coma decimal y hasta 2 decimales (ej: 1500 o 1500,50), mayores o iguales a 0.
    // No se aceptan puntos: en Argentina "1.500" es mil quinientos, y "1500.50"
    // se leería como 150050. Es más seguro rechazarlo.
    public static bool IntentarLeerDecimal(string? texto, out decimal valor)
    {
        return decimal.TryParse((texto ?? string.Empty).Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out valor)
            && valor >= 0
            && decimal.Round(valor, 2) == valor;
    }
}
