using System.Globalization;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Arma el texto del ticket (no fiscal) de una venta, línea por línea,
/// con el ancho exacto del papel. No sabe nada de impresoras: solo devuelve las líneas.
/// </summary>
public static class TicketVenta
{
    // El ticket es un comprobante argentino: los montos van siempre en formato es-AR ($ 1.500,00),
    // aunque en el futuro la pantalla esté en otro idioma
    private static readonly CultureInfo Argentina = new("es-AR");

    // La venta tiene que venir con Detalles, MedioPago y Usuario cargados
    public static IReadOnlyList<string> Armar(Venta venta, DatosComercio comercio, int ancho, bool esReimpresion)
    {
        var lineas = new List<string>();
        var separador = new string('-', ancho);

        // Encabezado del comercio
        Centrar(lineas, comercio.Nombre.ToUpper(Argentina), ancho);
        Centrar(lineas, comercio.Direccion, ancho);
        if (!string.IsNullOrWhiteSpace(comercio.Cuit))
            Centrar(lineas, $"CUIT {comercio.Cuit.Trim()}", ancho);
        if (!string.IsNullOrWhiteSpace(comercio.Telefono))
            Centrar(lineas, $"Tel. {comercio.Telefono.Trim()}", ancho);

        if (esReimpresion)
            Centrar(lineas, "*** REIMPRESIÓN ***", ancho);

        lineas.Add(separador);
        DosColumnas(lineas, $"Venta N° {venta.Id}", venta.Fecha.ToString("dd/MM/yyyy HH:mm", Argentina), ancho);
        AjustarTexto(lineas, $"Cajero: {venta.Usuario?.NombreCompleto}", ancho);
        lineas.Add(separador);

        // Productos: "2 x $ 2.500,00" y abajo el nombre con el subtotal a la derecha
        foreach (var detalle in venta.Detalles)
        {
            AjustarTexto(lineas, $"{detalle.Cantidad} x {Dinero(detalle.PrecioUnitario)}", ancho);
            DosColumnas(lineas, detalle.NombreProducto, Dinero(detalle.Subtotal), ancho);
        }

        lineas.Add(separador);

        if (venta.Descuento > 0)
        {
            DosColumnas(lineas, "Subtotal", Dinero(venta.Subtotal), ancho);
            DosColumnas(lineas, $"Descuento {venta.DescuentoPorcentaje.ToString("0.##", Argentina)}%", $"-{Dinero(venta.Descuento)}", ancho);
        }

        DosColumnas(lineas, "TOTAL", Dinero(venta.Total), ancho);

        // Medio de pago: en efectivo, con cuánto pagó y el vuelto
        var medio = venta.MedioPago?.Nombre ?? string.Empty;
        DosColumnas(lineas, medio, Dinero(venta.MontoRecibido ?? venta.Total), ancho);
        if (venta.Vuelto is decimal vuelto)
            DosColumnas(lineas, "Vuelto", Dinero(vuelto), ancho);

        lineas.Add(separador);

        // Obligatorio mientras el ticket no sea fiscal (la factura con ARCA llega en la Fase 6)
        Centrar(lineas, "NO VÁLIDO COMO FACTURA", ancho);
        Centrar(lineas, comercio.MensajePie, ancho);

        return lineas;
    }

    private static string Dinero(decimal monto) => "$ " + monto.ToString("N2", Argentina);

    // Texto centrado. Si no entra en una línea, se reparte en varias. Si está vacío, no agrega nada.
    private static void Centrar(List<string> lineas, string? texto, int ancho)
    {
        foreach (var parte in Partir(texto, ancho))
        {
            var izquierda = (ancho - parte.Length) / 2;
            lineas.Add(new string(' ', izquierda) + parte);
        }
    }

    // Texto a la izquierda, repartido en varias líneas si es largo
    private static void AjustarTexto(List<string> lineas, string? texto, int ancho)
        => lineas.AddRange(Partir(texto, ancho));

    // Texto a la izquierda y monto a la derecha. Si el texto es largo, se reparte
    // en varias líneas y el monto va en la última.
    private static void DosColumnas(List<string> lineas, string izquierda, string derecha, int ancho)
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
    private static List<string> Partir(string? texto, int ancho)
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
