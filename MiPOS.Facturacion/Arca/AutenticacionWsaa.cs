using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace MiPOS.Facturacion.Arca;

// Permiso de acceso que da ARCA (el "ticket de acceso"): dura 12 horas
public record CredencialesArca(string Token, string Sign, DateTimeOffset Vencimiento);

/// <summary>
/// WSAA: el servicio de autenticación de ARCA. Para usar la factura electrónica hay que pedirle
/// un permiso de acceso firmando un pedido con el certificado digital del comercio.
/// El permiso se guarda en memoria y en un archivo, porque ARCA no da otro mientras el anterior siga vigente.
/// </summary>
public class AutenticacionWsaa
{
    private const string Servicio = "wsfe";

    private const string UrlHomologacion = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
    private const string UrlProduccion = "https://wsaa.afip.gov.ar/ws/services/LoginCms";

    private static readonly XNamespace Wsaa = "http://wsaa.view.sua.dvadac.desein.afip.gov";

    // Se renueva un poco antes de que venza, para no quedarse sin permiso en medio de una venta
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromMinutes(10);

    // Un solo pedido de permiso a la vez en toda la aplicación
    private static readonly SemaphoreSlim Candado = new(1, 1);

    // Permisos vigentes por archivo (uno para homologación y otro para producción)
    private static readonly Dictionary<string, CredencialesArca> EnMemoria = new();

    private readonly OpcionesFacturacion _opciones;

    public AutenticacionWsaa(OpcionesFacturacion opciones)
    {
        _opciones = opciones;
    }

    private string RutaArchivo => Path.Combine(_opciones.RutaCarpetaDatos, $"acceso-{Servicio}-{_opciones.Modo}.xml");

    public async Task<CredencialesArca> ObtenerCredencialesAsync(CancellationToken cancelacion = default)
    {
        await Candado.WaitAsync(cancelacion);
        try
        {
            if (EnMemoria.TryGetValue(RutaArchivo, out var enMemoria) && EsVigente(enMemoria))
                return enMemoria;

            var credenciales = LeerArchivo();
            if (!EsVigente(credenciales))
            {
                credenciales = await PedirAsync(cancelacion);
                GuardarArchivo(credenciales);
            }

            EnMemoria[RutaArchivo] = credenciales!;
            return credenciales!;
        }
        finally
        {
            Candado.Release();
        }
    }

    // Si ARCA rechaza el permiso (por ejemplo, porque se cambió el certificado), se descarta para pedir otro
    public void Descartar()
    {
        EnMemoria.Remove(RutaArchivo);
        try { File.Delete(RutaArchivo); } catch (IOException) { }
    }

    private static bool EsVigente(CredencialesArca? credenciales)
        => credenciales is not null && credenciales.Vencimiento - MargenRenovacion > DateTimeOffset.Now;

    private async Task<CredencialesArca> PedirAsync(CancellationToken cancelacion)
    {
        using var certificado = CargarCertificado(_opciones);
        var tra = ArmarPedido(DateTimeOffset.Now, Servicio);
        var firmado = Firmar(tra, certificado);

        var url = _opciones.Modo == ModoFacturacion.Produccion ? UrlProduccion : UrlHomologacion;
        var cuerpo = new XElement(Wsaa + "loginCms", new XElement(Wsaa + "in0", firmado));

        XElement respuesta;
        try
        {
            respuesta = await ClienteSoap.EnviarAsync(url, string.Empty, cuerpo, _opciones.TiempoEsperaSegundos, cancelacion);
        }
        catch (ErrorSoapException ex) when (ex.Codigo.Contains("alreadyAuthenticated", StringComparison.OrdinalIgnoreCase))
        {
            // Ya hay un permiso vigente que se perdió (por ejemplo, se borró el archivo).
            // ARCA no da otro hasta que venza: hay que esperar.
            throw new FacturacionException(
                "ARCA indica que esta PC ya tiene un permiso de acceso vigente que se perdió. Vuelva a intentar en unos minutos.",
                reintentable: true, ex);
        }
        catch (ErrorSoapException ex)
        {
            throw new FacturacionException($"ARCA rechazó el acceso: {ex.Message} (revise el certificado y que esté asociado al servicio de factura electrónica).", causa: ex);
        }

        return LeerRespuesta(respuesta.Value);
    }

    /// <summary>
    /// El pedido de acceso (TRA): para qué servicio y por cuánto tiempo. Va firmado con el certificado.
    /// </summary>
    public static string ArmarPedido(DateTimeOffset ahora, string servicio)
    {
        // La hora de la PC puede estar un poco adelantada o atrasada respecto de ARCA:
        // se pide desde 10 minutos antes hasta 10 minutos después
        var documento = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest", new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", ahora.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                    new XElement("generationTime", ahora.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)),
                    new XElement("expirationTime", ahora.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))),
                new XElement("service", servicio)));

        return documento.Declaration + documento.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Firma el pedido con el certificado (formato CMS / PKCS#7) y lo devuelve en Base64, como lo pide ARCA.
    /// </summary>
    public static string Firmar(string pedido, X509Certificate2 certificado)
    {
        if (!certificado.HasPrivateKey)
            throw new FacturacionException("El certificado de ARCA no tiene la clave privada: no se puede firmar.");

        var contenido = new ContentInfo(Encoding.UTF8.GetBytes(pedido));
        var firma = new SignedCms(contenido, detached: false);
        var firmante = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificado)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };

        try
        {
            firma.ComputeSignature(firmante);
        }
        catch (CryptographicException ex)
        {
            throw new FacturacionException($"No se pudo firmar con el certificado de ARCA: {ex.Message}", causa: ex);
        }

        return Convert.ToBase64String(firma.Encode());
    }

    /// <summary>
    /// Lee la respuesta de ARCA (un XML dentro del XML) con el permiso de acceso.
    /// </summary>
    public static CredencialesArca LeerRespuesta(string xmlRespuesta)
    {
        try
        {
            var raiz = XDocument.Parse(xmlRespuesta).Root!;
            var token = raiz.Element("credentials")?.Element("token")?.Value;
            var sign = raiz.Element("credentials")?.Element("sign")?.Value;
            var vencimiento = raiz.Element("header")?.Element("expirationTime")?.Value;

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(sign) || vencimiento is null)
                throw new FormatException("Faltan datos en la respuesta.");

            return new CredencialesArca(token, sign, DateTimeOffset.Parse(vencimiento, CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or NullReferenceException)
        {
            throw new FacturacionException("La respuesta de acceso de ARCA no se pudo leer.", reintentable: true, ex);
        }
    }

    public static X509Certificate2 CargarCertificado(OpcionesFacturacion opciones)
    {
        try
        {
            var ruta = opciones.RutaCertificado;

            if (ruta.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) || ruta.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            {
                return X509CertificateLoader.LoadPkcs12FromFile(ruta,
                    string.IsNullOrEmpty(opciones.ContrasenaCertificado) ? null : opciones.ContrasenaCertificado);
            }

            // Certificado (.crt) y clave (.key) en archivos de texto, como los entrega ARCA.
            // Se pasan por PKCS#12 porque en Windows la firma no funciona con claves "temporales".
            using var temporal = X509Certificate2.CreateFromPemFile(ruta, opciones.RutaClavePrivada);
            return X509CertificateLoader.LoadPkcs12(temporal.Export(X509ContentType.Pkcs12), null);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new FacturacionException($"No se pudo leer el certificado de ARCA: {ex.Message}", causa: ex);
        }
    }

    // ----- Archivo con el permiso vigente -----

    private CredencialesArca? LeerArchivo()
    {
        try
        {
            if (!File.Exists(RutaArchivo))
                return null;

            var raiz = XDocument.Load(RutaArchivo).Root!;
            return new CredencialesArca(
                raiz.Element("token")!.Value,
                raiz.Element("sign")!.Value,
                DateTimeOffset.Parse(raiz.Element("vencimiento")!.Value, CultureInfo.InvariantCulture));
        }
        catch (Exception)
        {
            // Si el archivo está dañado, se pide un permiso nuevo
            return null;
        }
    }

    private void GuardarArchivo(CredencialesArca credenciales)
    {
        try
        {
            Directory.CreateDirectory(_opciones.RutaCarpetaDatos);
            new XElement("acceso",
                new XElement("token", credenciales.Token),
                new XElement("sign", credenciales.Sign),
                new XElement("vencimiento", credenciales.Vencimiento.ToString("O", CultureInfo.InvariantCulture)))
                .Save(RutaArchivo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No es grave: el permiso sigue en memoria mientras el programa esté abierto
        }
    }
}
