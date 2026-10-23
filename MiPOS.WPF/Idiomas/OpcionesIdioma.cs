namespace MiPOS.WPF.Idiomas;

/// <summary>
/// Idioma de esta caja. Sección "Idioma" de appsettings.json.
/// Se usa en el inicio de sesión y para los usuarios que no eligieron uno propio.
/// </summary>
public class OpcionesIdioma
{
    // "es" (español) o "zh" (chino)
    public string Predeterminado { get; set; } = Traductor.Espanol;
}
