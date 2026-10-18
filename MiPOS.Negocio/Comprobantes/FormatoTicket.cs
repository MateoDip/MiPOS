using System.Globalization;

namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Funciones para armar las líneas de un ticket de ancho fijo. Las usan todos los tickets
/// (venta, cierre de caja) para que se vean iguales.
/// </summary>
public static class FormatoTicket
{
    // El ticket es un comprobante argentino: los montos van siempre en formato es-AR ($ 1.500,00),
    // aunque en el futuro la pantalla esté en otro idioma
    public static readonly CultureInfo Argentina = new("es-AR");

    // Nombre, dirección, CUIT y teléfono del comercio, centrados. Los vacíos no se imprimen.
    public static void EncabezadoComercio(List<string> lineas, DatosComercio comercio, int ancho)
    {
        Centrar(lineas, comercio.Nombre.ToUpper(Argentina), ancho);
        Centrar(lineas, comercio.Direccion, ancho);
        if (!string.IsNullOrWhiteSpace(comercio.Cuit))
            Centrar(lineas, $"CUIT {comercio.Cuit.Trim()}", ancho);
        if (!string.IsNullOrWhiteSpace(comercio.Telefono))
            Centrar(lineas, $"Tel. {comercio.Telefono.Trim()}", ancho);
    }

    // Monto con signo adelante: "-$ 100,00" o "+$ 100,00" (cero sin signo)
    public static string DineroConSigno(decimal monto)
        => monto switch
        {
            < 0 => "-" + Dinero(-monto),
            > 0 => "+" + Dinero(monto),
            _ => Dinero(0)
        };

    public static string Dinero(decimal monto) => "$ " + monto.ToString("N2", Argentina);

    // Texto centrado. Si no entra en una línea, se reparte en varias. Si está vacío, no agrega nada.
    public static void Centrar(List<string> lineas, string? texto, int ancho)
    {
        foreach (var parte in Partir(texto, ancho))
        {
            var izquierda = (ancho - parte.Length) / 2;
            lineas.Add(new string(' ', izquierda) + parte);
        }
    }

    // Texto a la izquierda, repartido en varias líneas si es largo
    public static void AjustarTexto(List<string> lineas, string? texto, int ancho)
        => lineas.AddRange(Partir(texto, ancho));

    // Texto a la izquierda y monto a la derecha. Si el texto es largo, se reparte
    // en varias líneas y el monto va en la última.
    public static void DosColumnas(List<string> lineas, string izquierda, string derecha, int ancho)
    {
        var anchoIzquierda = Math.Max(1, ancho - derecha.Length - 1);
        var partes = Partir(izquierda, anchoIzquierda);
        if (partes.Count == 0)
            partes.Add(string.Empty);

        for (var i = 0; i < partes.Count - 1; i++)
            lineas.Add(partes[i]);

        lineas.Add(partes[^1].PadRight(ancho - derecha.Length) + derecha);
    }

    // Reparte un texto en líneas de hasta "ancho" letras, cortando entre palabras cuando se puede
    public static List<string> Partir(string? texto, int ancho)
    {
        var resultado = new List<string>();
        if (string.IsNullOrWhiteSpace(texto))
            return resultado;

        var actual = string.Empty;
        foreach (var palabraOriginal in texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var palabra = palabraOriginal;

            // Una palabra más larga que la línea se corta a la fuerza
            while (palabra.Length > ancho)
            {
                if (actual.Length > 0)
                {
                    resultado.Add(actual);
                    actual = string.Empty;
                }
                resultado.Add(palabra[..ancho]);
                palabra = palabra[ancho..];
            }

            if (actual.Length == 0)
                actual = palabra;
            else if (actual.Length + 1 + palabra.Length <= ancho)
                actual += " " + palabra;
            else
            {
                resultado.Add(actual);
                actual = palabra;
            }
        }

        if (actual.Length > 0)
            resultado.Add(actual);

        return resultado;
    }
}
