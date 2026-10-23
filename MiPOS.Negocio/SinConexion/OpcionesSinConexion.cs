namespace MiPOS.Negocio.SinConexion;

/// <summary>
/// Venta sin conexión con la base central. Sección "SinConexion" de appsettings.json.
/// </summary>
public class OpcionesSinConexion
{
    // false = si se corta la conexión, no se puede vender (como antes de la Fase 8)
    public bool Activo { get; set; } = true;

    // Archivo SQLite de esta caja. Una ruta relativa es desde la carpeta del programa.
    public string ArchivoBase { get; set; } = @"DatosLocales\caja.db";

    // Cada cuánto se prueba la conexión y se suben las ventas pendientes
    public int SegundosEntreIntentos { get; set; } = 30;

    // Cada cuánto se descarga de nuevo la copia de productos, medios de pago y usuarios
    public int MinutosEntreDescargas { get; set; } = 10;

    public string RutaCompleta => Path.IsPathRooted(ArchivoBase)
        ? ArchivoBase
        : Path.Combine(AppContext.BaseDirectory, ArchivoBase);
}
