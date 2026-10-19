namespace MiPOS.Hardware.Impresion;

/// <summary>
/// Algo que puede imprimir un ticket: una impresora de Windows (ImpresoraWindows)
/// o un archivo de texto para probar sin impresora (ImpresoraArchivo).
/// Cuál se usa lo decide la configuración (Impresion:Modo).
/// </summary>
public interface IImpresoraTicket
{
    // El ticket se arma con este ancho, para que cada línea entre justa en el papel
    int CaracteresPorLinea { get; }

    // Lanza ImpresionException si no se pudo imprimir.
    // codigoQr: texto del código QR que se dibuja al final (las facturas electrónicas lo exigen); null = sin QR.
    Task ImprimirAsync(IReadOnlyList<string> lineas, string nombreTrabajo, string? codigoQr = null);
}
