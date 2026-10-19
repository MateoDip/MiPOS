using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QRCoder;

namespace MiPOS.Hardware.Impresion;

/// <summary>
/// Imprime en cualquier impresora instalada en Windows (térmica, láser, etc.) usando su driver.
/// </summary>
public class ImpresoraWindows : IImpresoraTicket
{
    // Windows mide en "unidades independientes": 96 por pulgada (25,4 mm)
    private const double UnidadesPorMm = 96 / 25.4;

    // Letra de ancho fijo: todas las letras ocupan lo mismo, así las columnas quedan alineadas
    private static readonly FontFamily Fuente = new("Consolas");

    // Lado del código QR: ARCA pide que se pueda leer bien con un celular (entre 25 y 35 mm es lo habitual)
    private const double LadoQrMm = 30;

    private readonly OpcionesImpresion _opciones;

    public ImpresoraWindows(OpcionesImpresion opciones)
    {
        _opciones = opciones;
    }

    public int CaracteresPorLinea => _opciones.CaracteresPorLinea;

    public Task ImprimirAsync(IReadOnlyList<string> lineas, string nombreTrabajo, string? codigoQr = null)
    {
        _opciones.Validar();

        // La impresión de WPF necesita un hilo "STA". Se usa uno nuevo para que la caja
        // siga respondiendo mientras Windows prepara el trabajo.
        var resultado = new TaskCompletionSource();
        var hilo = new Thread(() =>
        {
            try
            {
                Imprimir(lineas, nombreTrabajo, codigoQr);
                resultado.SetResult();
            }
            catch (Exception ex)
            {
                resultado.SetException(ex as ImpresionException
                    ?? new ImpresionException($"Error al imprimir: {ex.Message}", ex));
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.IsBackground = true;
        hilo.Start();

        return resultado.Task;
    }

    private void Imprimir(IReadOnlyList<string> lineas, string nombreTrabajo, string? codigoQr)
    {
        using var servidor = new LocalPrintServer();
        using var cola = BuscarImpresora(servidor);

        // Si el driver informa el estado, se avisa antes de mandar el trabajo
        cola.Refresh();
        if (cola.IsOffline)
            throw new ImpresionException($"La impresora \"{cola.Name}\" está desconectada o apagada.");
        if (cola.IsOutOfPaper)
            throw new ImpresionException($"La impresora \"{cola.Name}\" no tiene papel.");

        var ancho = _opciones.AnchoPapelMm * UnidadesPorMm;
        var margen = _opciones.MargenMm * UnidadesPorMm;
        var tamanoLetra = CalcularTamanoLetra(ancho - 2 * margen);
        var altoLinea = tamanoLetra * 1.2;

        // El QR no puede ser más ancho que el papel
        var ladoQr = codigoQr is null ? 0 : Math.Min(LadoQrMm * UnidadesPorMm, ancho - 2 * margen);

        var ticketImpresion = (cola.UserPrintTicket ?? cola.DefaultPrintTicket).Clone();

        double alto;
        if (_opciones.PapelContinuo)
        {
            // Rollo: la página mide exactamente lo que ocupa el ticket
            alto = 2 * margen + (lineas.Count + 1) * altoLinea + (codigoQr is null ? 0 : ladoQr + altoLinea);
            ticketImpresion.PageMediaSize = new PageMediaSize(ancho, alto);
        }
        else
        {
            // Hojas: se usa el alto del papel configurado en la impresora (A4 si no informa)
            alto = ticketImpresion.PageMediaSize?.Height ?? 297 * UnidadesPorMm;
        }

        var documento = ArmarDocumento(lineas, ancho, alto, margen, tamanoLetra, altoLinea);
        if (codigoQr is not null)
            documento.Blocks.Add(ArmarQr(codigoQr, ladoQr, altoLinea));

        cola.CurrentJobSettings.Description = nombreTrabajo;
        var escritor = PrintQueue.CreateXpsDocumentWriter(cola);
        escritor.Write(((IDocumentPaginatorSource)documento).DocumentPaginator, ticketImpresion);
    }

    private PrintQueue BuscarImpresora(LocalPrintServer servidor)
    {
        try
        {
            return string.IsNullOrWhiteSpace(_opciones.Impresora)
                ? LocalPrintServer.GetDefaultPrintQueue()
                : servidor.GetPrintQueue(_opciones.Impresora.Trim());
        }
        catch (Exception ex) when (ex is PrintQueueException or PrintSystemException)
        {
            var cual = string.IsNullOrWhiteSpace(_opciones.Impresora)
                ? "No hay una impresora predeterminada en Windows."
                : $"No se encontró la impresora \"{_opciones.Impresora}\".";

            throw new ImpresionException($"{cual} Revise Impresion:Impresora en appsettings.json.", ex);
        }
    }

    // Tamaño de letra para que entren justo CaracteresPorLinea letras en el ancho útil
    private double CalcularTamanoLetra(double anchoUtil)
    {
        var muestra = new FormattedText(
            new string('M', _opciones.CaracteresPorLinea),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(Fuente, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            1.0,
            Brushes.Black,
            1.0);

        // 2% de margen de seguridad para que ninguna línea se corte en dos
        return anchoUtil / muestra.WidthIncludingTrailingWhitespace * 0.98;
    }

    // El código QR centrado, como imagen
    private static BlockUIContainer ArmarQr(string texto, double lado, double espacioArriba)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(datos).GetGraphic(10);

        var imagen = new BitmapImage();
        imagen.BeginInit();
        imagen.CacheOption = BitmapCacheOption.OnLoad;
        imagen.StreamSource = new MemoryStream(png);
        imagen.EndInit();
        imagen.Freeze();

        return new BlockUIContainer(new Image
        {
            Source = imagen,
            Width = lado,
            Height = lado,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        })
        {
            Margin = new Thickness(0, espacioArriba, 0, 0)
        };
    }

    private static FlowDocument ArmarDocumento(IReadOnlyList<string> lineas, double ancho, double alto,
                                               double margen, double tamanoLetra, double altoLinea)
    {
        var parrafo = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = altoLinea,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        for (var i = 0; i < lineas.Count; i++)
        {
            parrafo.Inlines.Add(new Run(lineas[i]));
            if (i < lineas.Count - 1)
                parrafo.Inlines.Add(new LineBreak());
        }

        return new FlowDocument(parrafo)
        {
            FontFamily = Fuente,
            FontSize = tamanoLetra,
            PageWidth = ancho,
            PageHeight = alto,
            PagePadding = new Thickness(margen),
            ColumnWidth = ancho, // una sola columna
            TextAlignment = TextAlignment.Left,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };
    }
}
