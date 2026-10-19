using static MiPOS.Negocio.Comprobantes.FormatoTicket;

namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Ticket para probar la impresora al instalarla: muestra si el ancho, los acentos y el QR salen bien.
/// </summary>
public static class TicketPrueba
{
    // Texto del QR de prueba (cualquier celular tiene que poder leerlo)
    public const string CodigoQr = "MiPOS - prueba de impresión";

    public static IReadOnlyList<string> Armar(DatosComercio comercio, int ancho)
    {
        var lineas = new List<string>();
        var separador = new string('-', ancho);

        EncabezadoComercio(lineas, comercio, ancho);
        Centrar(lineas, "PRUEBA DE IMPRESIÓN", ancho);
        lineas.Add(separador);
        DosColumnas(lineas, "Fecha", DateTime.Now.ToString("dd/MM/yyyy HH:mm", Argentina), ancho);
        AjustarTexto(lineas, $"Caracteres por línea: {ancho}", ancho);
        lineas.Add(separador);

        // Regla: la última cifra tiene que quedar justo en el borde derecho, sin cortarse ni pasar a otra línea
        lineas.Add(string.Concat(Enumerable.Range(1, ancho).Select(i => (i % 10).ToString())));
        AjustarTexto(lineas, "La regla de arriba debe ocupar una sola línea completa.", ancho);
        lineas.Add(separador);

        AjustarTexto(lineas, "Acentos: áéíóú ÁÉÍÓÚ ñÑ ü ¿? ¡!", ancho);
        DosColumnas(lineas, "Monto", Dinero(1234567.89m), ancho);
        lineas.Add(separador);
        AjustarTexto(lineas, "Abajo tiene que salir un código QR que se pueda leer con el celular.", ancho);

        return lineas;
    }
}
