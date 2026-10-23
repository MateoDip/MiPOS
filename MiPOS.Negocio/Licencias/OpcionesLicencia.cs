namespace MiPOS.Negocio.Licencias;

/// <summary>
/// Dónde está el archivo de licencia de esta PC. Sección "Licencia" de appsettings.json.
/// </summary>
public class OpcionesLicencia
{
    // Una ruta relativa es desde la carpeta del programa
    public string Archivo { get; set; } = "licencia.lic";

    public string RutaCompleta => Path.IsPathRooted(Archivo)
        ? Archivo
        : Path.Combine(AppContext.BaseDirectory, Archivo);
}
