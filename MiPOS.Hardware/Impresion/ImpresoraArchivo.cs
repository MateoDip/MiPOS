using System.IO;
using System.Text;

namespace MiPOS.Hardware.Impresion;

/// <summary>
/// "Imprime" guardando el ticket como archivo de texto. Sirve para desarrollar y probar sin impresora.
/// </summary>
public class ImpresoraArchivo : IImpresoraTicket
{
    private readonly OpcionesImpresion _opciones;

    public ImpresoraArchivo(OpcionesImpresion opciones)
    {
        _opciones = opciones;
    }

    public int CaracteresPorLinea => _opciones.CaracteresPorLinea;

    // Ruta del último archivo guardado (para mostrarla en pruebas)
    public string? UltimoArchivo { get; private set; }

    public async Task ImprimirAsync(IReadOnlyList<string> lineas, string nombreTrabajo)
    {
        _opciones.Validar();

        try
        {
            var carpeta = Path.IsPathRooted(_opciones.CarpetaArchivos)
                ? _opciones.CarpetaArchivos
                : Path.Combine(AppContext.BaseDirectory, _opciones.CarpetaArchivos);

            Directory.CreateDirectory(carpeta);

            // Ej: "Venta 15" -> "2026-10-12_14-32-05_Venta_15.txt"
            var nombre = string.Concat(nombreTrabajo.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
            var ruta = Path.Combine(carpeta, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{nombre}.txt");

            await File.WriteAllLinesAsync(ruta, lineas, Encoding.UTF8);
            UltimoArchivo = ruta;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImpresionException($"No se pudo guardar el ticket en la carpeta \"{_opciones.CarpetaArchivos}\": {ex.Message}", ex);
        }
    }
}
