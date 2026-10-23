using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MiPOS.WPF.Idiomas;

/// <summary>
/// Traduce los textos de la interfaz. El programa está escrito en español y cada idioma tiene un catálogo
/// (Idiomas/zh.json) donde la clave es el texto en español y el valor, la traducción.
/// Los textos con datos que cambian se escriben con {0}, {1}...: "Venta N° {0} registrada." sirve para
/// cualquier número. Los datos también se traducen si están en el catálogo (por ejemplo, un mensaje de error
/// metido dentro de otro). Lo que no está en el catálogo se muestra en español: nunca falla.
/// Los comprobantes (tickets, facturas) siempre salen en español: son documentos legales en Argentina.
/// </summary>
public static class Traductor
{
    public const string Espanol = "es";

    // Idiomas que tiene el programa: código y nombre (en su propio idioma)
    public static readonly IReadOnlyList<(string Codigo, string Nombre)> Disponibles = new[]
    {
        (Espanol, "Español"),
        ("zh", "中文")
    };

    private static Dictionary<string, string> _exactos = new();
    private static List<Plantilla> _plantillas = new();
    private static readonly ConcurrentDictionary<string, string> _yaTraducidos = new();

    // Un texto con datos: el patrón para reconocerlo y la traducción con los mismos {0}, {1}...
    private sealed record Plantilla(Regex Patron, string Traduccion, int LargoFijo);

    public static string Idioma { get; private set; } = Espanol;

    public static bool EsEspanol => Idioma == Espanol;

    /// <summary>
    /// Cambia el idioma de las ventanas que se abran desde ahora. Un código desconocido (o null) deja español.
    /// </summary>
    public static void Usar(string? idioma)
    {
        idioma = string.IsNullOrWhiteSpace(idioma) ? Espanol : idioma.Trim().ToLowerInvariant();
        if (idioma == Idioma)
            return;

        _yaTraducidos.Clear();

        var catalogo = idioma == Espanol ? null : LeerCatalogo(idioma);
        if (catalogo is null)
        {
            _exactos = new();
            _plantillas = new();
            Idioma = Espanol;
            return;
        }

        _exactos = catalogo.Where(t => !t.Key.Contains('{')).ToDictionary(t => t.Key, t => t.Value);

        // Las plantillas más específicas (más texto fijo) se prueban primero
        _plantillas = catalogo
            .Where(t => Regex.IsMatch(t.Key, @"\{\d+\}"))
            .Select(t => new Plantilla(
                new Regex("^" + Regex.Replace(Regex.Escape(t.Key), @"\\\{(\d+)}", "(?<p$1>.+?)") + "$", RegexOptions.Singleline),
                t.Value,
                Regex.Replace(t.Key, @"\{\d+\}", string.Empty).Length))
            .OrderByDescending(p => p.LargoFijo)
            .ToList();

        Idioma = idioma;
    }

    public static string Traducir(string? texto)
    {
        if (string.IsNullOrEmpty(texto) || EsEspanol)
            return texto ?? string.Empty;

        if (_exactos.TryGetValue(texto, out var exacto))
            return exacto;

        if (_yaTraducidos.TryGetValue(texto, out var guardado))
            return guardado;

        var resultado = texto;
        foreach (var plantilla in _plantillas)
        {
            var coincidencia = plantilla.Patron.Match(texto);
            if (!coincidencia.Success)
                continue;

            // Cada dato también se traduce (si es un texto conocido)
            resultado = Regex.Replace(plantilla.Traduccion, @"\{(\d+)\}",
                m => Traducir(coincidencia.Groups["p" + m.Groups[1].Value].Value));
            break;
        }

        // Los mensajes con números distintos son muchos: no se guardan para siempre
        if (_yaTraducidos.Count > 5000)
            _yaTraducidos.Clear();
        _yaTraducidos[texto] = resultado;

        return resultado;
    }

    // El catálogo va dentro del programa (EmbeddedResource en MiPOS.WPF.csproj)
    private static Dictionary<string, string>? LeerCatalogo(string idioma)
    {
        using var flujo = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Idiomas.{idioma}.json");
        if (flujo is null)
            return null;

        var catalogo = JsonSerializer.Deserialize<Dictionary<string, string>>(flujo) ?? new();

        // Las claves que empiezan con "_" son comentarios
        return catalogo.Where(t => !t.Key.StartsWith('_')).ToDictionary(t => t.Key, t => t.Value);
    }
}
