namespace MiPOS.Negocio.Comun;

/// <summary>
/// CUIT / CUIL argentino: 11 dígitos, el último es un dígito verificador.
/// Lo usan los proveedores y la facturación.
/// </summary>
public static class Cuit
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>
    /// Acepta el CUIT con o sin guiones/espacios ("20-12345678-6" o "20123456786").
    /// Devuelve true y el CUIT solo con números si es válido (incluye el dígito verificador).
    /// </summary>
    public static bool IntentarNormalizar(string? texto, out string cuit)
    {
        cuit = new string((texto ?? string.Empty).Where(c => c is not ('-' or ' ')).ToArray());

        if (cuit.Length != 11 || !cuit.All(char.IsAsciiDigit))
            return false;

        var suma = 0;
        for (var i = 0; i < 10; i++)
            suma += (cuit[i] - '0') * Pesos[i];

        var verificador = 11 - suma % 11;
        if (verificador == 11) verificador = 0;

        // Si da 10, ARCA no emite ese número (asigna otro prefijo, como 23): no es válido
        return verificador != 10 && cuit[10] - '0' == verificador;
    }

    // "20123456786" -> "20-12345678-6"
    public static string Formatear(string cuit)
        => cuit.Length == 11 ? $"{cuit[..2]}-{cuit[2..10]}-{cuit[10]}" : cuit;
}
