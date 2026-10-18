namespace MiPOS.Hardware.Impresion;

public enum ModoImpresion
{
    // Imprime en una impresora instalada en Windows (térmica, láser, etc.)
    Windows,

    // Guarda el ticket como archivo de texto (para desarrollar o probar sin impresora)
    Archivo
}

/// <summary>
/// Configuración de la impresora de tickets. Se lee de la sección "Impresion" de appsettings.json,
/// porque cada caja puede tener una impresora distinta.
/// </summary>
public class OpcionesImpresion
{
    public ModoImpresion Modo { get; set; } = ModoImpresion.Windows;

    // Nombre de la impresora tal como aparece en Windows. Vacío = la impresora predeterminada.
    public string Impresora { get; set; } = string.Empty;

    // Ancho del papel: 80 o 58 en las térmicas, 210 en una hoja A4
    public double AnchoPapelMm { get; set; } = 80;

    // Cuántas letras entran en una línea. El tamaño de letra se calcula para que entren justo.
    public int CaracteresPorLinea { get; set; } = 42;

    public double MargenMm { get; set; } = 3;

    // true = rollo (el largo de la página se ajusta al ticket); false = hojas de tamaño fijo
    public bool PapelContinuo { get; set; } = true;

    // Solo para Modo = Archivo. Si no es una ruta completa, es relativa a la carpeta del programa.
    public string CarpetaArchivos { get; set; } = "Tickets";

    public void Validar()
    {
        if (CaracteresPorLinea < 24 || CaracteresPorLinea > 120)
            throw new ImpresionException("Configuración de impresión inválida: CaracteresPorLinea debe estar entre 24 y 120.");

        if (AnchoPapelMm < 40 || AnchoPapelMm > 300)
            throw new ImpresionException("Configuración de impresión inválida: AnchoPapelMm debe estar entre 40 y 300.");

        if (MargenMm < 0 || MargenMm * 2 >= AnchoPapelMm)
            throw new ImpresionException("Configuración de impresión inválida: MargenMm es demasiado grande para el ancho del papel.");
    }
}
