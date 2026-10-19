using System.Net.Http;
using System.Text;
using System.Xml.Linq;

namespace MiPOS.Facturacion.Arca;

/// <summary>
/// Envía un mensaje SOAP (XML por HTTPS) a un servicio web de ARCA y devuelve la respuesta.
/// Convierte los problemas de conexión en FacturacionException reintentables.
/// </summary>
internal static class ClienteSoap
{
    public static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    // Un único HttpClient para toda la aplicación (recomendación de .NET).
    // El tiempo de espera se controla con la cancelación de cada pedido.
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<XElement> EnviarAsync(string url, string accionSoap, XElement cuerpo,
                                                   int tiempoEsperaSegundos, CancellationToken cancelacion)
    {
        var sobre = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", Soap),
                new XElement(Soap + "Body", cuerpo)));

        using var pedido = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(sobre.Declaration + sobre.ToString(SaveOptions.DisableFormatting),
                Encoding.UTF8, "text/xml")
        };
        pedido.Headers.Add("SOAPAction", $"\"{accionSoap}\"");

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(tiempoEsperaSegundos));

        string texto;
        try
        {
            using var respuesta = await Http.SendAsync(pedido, limite.Token);
            texto = await respuesta.Content.ReadAsStringAsync(limite.Token);

            // Un error SOAP llega con código 500 pero con el detalle en el XML: se lee igual
            if (!respuesta.IsSuccessStatusCode && !texto.Contains("Fault", StringComparison.Ordinal))
                throw new FacturacionException(
                    $"ARCA respondió con un error ({(int)respuesta.StatusCode} {respuesta.ReasonPhrase}). Intente más tarde.",
                    reintentable: true);
        }
        catch (OperationCanceledException ex) when (!cancelacion.IsCancellationRequested)
        {
            throw new FacturacionException(
                $"ARCA no respondió en {tiempoEsperaSegundos} segundos. Intente más tarde.", reintentable: true, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FacturacionException(
                "No hay conexión con ARCA. Verifique la conexión a Internet.", reintentable: true, ex);
        }

        XElement cuerpoRespuesta;
        try
        {
            cuerpoRespuesta = XDocument.Parse(texto).Root?.Element(Soap + "Body")?.Elements().FirstOrDefault()
                              ?? throw new FormatException("La respuesta no tiene cuerpo SOAP.");
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException)
        {
            throw new FacturacionException("ARCA devolvió una respuesta que no se pudo leer. Intente más tarde.",
                reintentable: true, ex);
        }

        if (cuerpoRespuesta.Name == Soap + "Fault")
            throw new ErrorSoapException(
                cuerpoRespuesta.Element("faultcode")?.Value ?? string.Empty,
                cuerpoRespuesta.Element("faultstring")?.Value ?? "Error desconocido");

        return cuerpoRespuesta;
    }
}

/// <summary>
/// El servicio de ARCA respondió con un error SOAP (Fault). Quien llama decide qué significa.
/// </summary>
internal class ErrorSoapException : Exception
{
    public ErrorSoapException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}
