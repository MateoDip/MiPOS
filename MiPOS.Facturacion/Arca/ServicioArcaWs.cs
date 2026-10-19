using System.Globalization;
using System.Xml.Linq;

namespace MiPOS.Facturacion.Arca;

/// <summary>
/// WSFEv1: el servicio web de ARCA para la factura electrónica (comprobantes A, B y C).
/// Habla con los servidores de homologación o de producción según Facturacion:Modo.
/// </summary>
public class ServicioArcaWs : IServicioArca
{
    private const string UrlHomologacion = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
    private const string UrlProduccion = "https://servicios1.afip.gov.ar/wsfev1/service.asmx";

    public static readonly XNamespace Fe = "http://ar.gov.afip.dif.FEV1/";

    // Código de error de ARCA que indica que el permiso de acceso (token) ya no sirve
    private const int ErrorDeAcceso = 600;

    private readonly OpcionesFacturacion _opciones;
    private readonly AutenticacionWsaa _autenticacion;

    public ServicioArcaWs(OpcionesFacturacion opciones, AutenticacionWsaa autenticacion)
    {
        _opciones = opciones;
        _autenticacion = autenticacion;
    }

    private string Url => _opciones.Modo == ModoFacturacion.Produccion ? UrlProduccion : UrlHomologacion;

    public async Task<long> ObtenerUltimoNumeroAsync(long cuitEmisor, int puntoVenta, int tipoComprobante,
                                                     CancellationToken cancelacion = default)
    {
        var resultado = await LlamarAsync("FECompUltimoAutorizado", cuitEmisor, cancelacion,
            new XElement(Fe + "PtoVta", puntoVenta),
            new XElement(Fe + "CbteTipo", tipoComprobante));

        ExigirSinErrores(resultado);
        return long.Parse(resultado.Element(Fe + "CbteNro")?.Value ?? "0", CultureInfo.InvariantCulture);
    }

    public async Task<RespuestaCae> SolicitarCaeAsync(long cuitEmisor, SolicitudCae solicitud, CancellationToken cancelacion = default)
    {
        var resultado = await LlamarAsync("FECAESolicitar", cuitEmisor, cancelacion, ArmarPedidoCae(solicitud));
        return LeerRespuestaCae(resultado);
    }

    public async Task<ComprobanteConsultado?> ConsultarAsync(long cuitEmisor, int puntoVenta, int tipoComprobante, long numero,
                                                             CancellationToken cancelacion = default)
    {
        var resultado = await LlamarAsync("FECompConsultar", cuitEmisor, cancelacion,
            new XElement(Fe + "FeCompConsReq",
                new XElement(Fe + "CbteTipo", tipoComprobante),
                new XElement(Fe + "CbteNro", numero),
                new XElement(Fe + "PtoVta", puntoVenta)));

        return LeerConsulta(resultado);
    }

    // ----- Armado de los pedidos y lectura de las respuestas (públicos para poder probarlos) -----

    /// <summary>
    /// El pedido de CAE de un comprobante (FeCAEReq). El orden de los elementos es el que exige ARCA.
    /// </summary>
    public static XElement ArmarPedidoCae(SolicitudCae s)
    {
        var detalle = new XElement(Fe + "FECAEDetRequest",
            new XElement(Fe + "Concepto", 1), // 1 = productos
            new XElement(Fe + "DocTipo", s.DocTipo),
            new XElement(Fe + "DocNro", s.DocNumero),
            new XElement(Fe + "CbteDesde", s.Numero),
            new XElement(Fe + "CbteHasta", s.Numero),
            new XElement(Fe + "CbteFch", s.Fecha.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            new XElement(Fe + "ImpTotal", Importe(s.ImporteTotal)),
            new XElement(Fe + "ImpTotConc", Importe(0)),
            new XElement(Fe + "ImpNeto", Importe(s.ImporteNeto)),
            new XElement(Fe + "ImpOpEx", Importe(s.ImporteExento)),
            new XElement(Fe + "ImpTrib", Importe(0)),
            new XElement(Fe + "ImpIVA", Importe(s.ImporteIva)),
            new XElement(Fe + "MonId", "PES"),
            new XElement(Fe + "MonCotiz", 1),
            new XElement(Fe + "CondicionIVAReceptorId", s.CondicionIvaReceptor));

        if (s.Alicuotas.Count > 0)
        {
            detalle.Add(new XElement(Fe + "Iva",
                s.Alicuotas.Select(a => new XElement(Fe + "AlicIva",
                    new XElement(Fe + "Id", a.Id),
                    new XElement(Fe + "BaseImp", Importe(a.BaseImponible)),
                    new XElement(Fe + "Importe", Importe(a.Importe))))));
        }

        return new XElement(Fe + "FeCAEReq",
            new XElement(Fe + "FeCabReq",
                new XElement(Fe + "CantReg", 1),
                new XElement(Fe + "PtoVta", s.PuntoVenta),
                new XElement(Fe + "CbteTipo", s.TipoComprobante)),
            new XElement(Fe + "FeDetReq", detalle));
    }

    // FECAESolicitarResult
    public static RespuestaCae LeerRespuestaCae(XElement resultado)
    {
        var detalle = resultado.Element(Fe + "FeDetResp")?.Element(Fe + "FECAEDetResponse");
        var aprobado = (detalle?.Element(Fe + "Resultado")?.Value ?? resultado.Element(Fe + "FeCabResp")?.Element(Fe + "Resultado")?.Value) == "A";

        var mensajes = LeerMensajes(resultado.Element(Fe + "Errors"), "Err")
            .Concat(LeerMensajes(detalle?.Element(Fe + "Observaciones"), "Obs"))
            .ToList();

        var cae = detalle?.Element(Fe + "CAE")?.Value;
        var vencimiento = LeerFecha(detalle?.Element(Fe + "CAEFchVto")?.Value);

        if (aprobado && (string.IsNullOrWhiteSpace(cae) || vencimiento is null))
        {
            aprobado = false;
            mensajes.Add((0, "ARCA aprobó el comprobante pero no informó el CAE."));
        }

        return new RespuestaCae(
            aprobado,
            aprobado ? cae : null,
            aprobado ? vencimiento : null,
            string.Join(" | ", mensajes.Select(m => m.Codigo > 0 ? $"{m.Codigo}: {m.Mensaje}" : m.Mensaje)),
            mensajes.Select(m => m.Codigo).Where(c => c > 0).ToList());
    }

    // FECompConsultarResult. null si el comprobante no existe (ARCA responde con el error 602).
    public static ComprobanteConsultado? LeerConsulta(XElement resultado)
    {
        var datos = resultado.Element(Fe + "ResultGet");
        if (datos is null)
        {
            var errores = LeerMensajes(resultado.Element(Fe + "Errors"), "Err").ToList();
            if (errores.Count == 0 || errores.Any(e => e.Codigo == 602))
                return null;

            throw new FacturacionException($"ARCA respondió con un error: {string.Join(" | ", errores.Select(e => $"{e.Codigo}: {e.Mensaje}"))}",
                reintentable: true);
        }

        return new ComprobanteConsultado(
            long.Parse(datos.Element(Fe + "CbteDesde")?.Value ?? "0", CultureInfo.InvariantCulture),
            datos.Element(Fe + "CodAutorizacion")?.Value ?? string.Empty,
            LeerFecha(datos.Element(Fe + "FchVto")?.Value) ?? default,
            LeerFecha(datos.Element(Fe + "CbteFch")?.Value) ?? default,
            decimal.Parse(datos.Element(Fe + "ImpTotal")?.Value ?? "0", CultureInfo.InvariantCulture),
            long.Parse(datos.Element(Fe + "DocNro")?.Value ?? "0", CultureInfo.InvariantCulture));
    }

    // ----- Ayudas -----

    private async Task<XElement> LlamarAsync(string metodo, long cuitEmisor, CancellationToken cancelacion, params object[] contenido)
    {
        // Si ARCA dice que el permiso ya no sirve, se pide uno nuevo y se intenta una vez más
        for (var intento = 1; ; intento++)
        {
            var credenciales = await _autenticacion.ObtenerCredencialesAsync(cancelacion);

            var cuerpo = new XElement(Fe + metodo,
                new XElement(Fe + "Auth",
                    new XElement(Fe + "Token", credenciales.Token),
                    new XElement(Fe + "Sign", credenciales.Sign),
                    new XElement(Fe + "Cuit", cuitEmisor)),
                contenido);

            XElement respuesta;
            try
            {
                respuesta = await ClienteSoap.EnviarAsync(Url, Fe.NamespaceName + metodo, cuerpo,
                    _opciones.TiempoEsperaSegundos, cancelacion);
            }
            catch (ErrorSoapException ex)
            {
                throw new FacturacionException($"ARCA respondió con un error: {ex.Message}", reintentable: true, ex);
            }

            var resultado = respuesta.Element(Fe + metodo + "Result")
                            ?? throw new FacturacionException("La respuesta de ARCA no se pudo leer.", reintentable: true);

            var errores = LeerMensajes(resultado.Element(Fe + "Errors"), "Err").ToList();
            if (intento == 1 && errores.Any(e => e.Codigo == ErrorDeAcceso))
            {
                _autenticacion.Descartar();
                continue;
            }

            return resultado;
        }
    }

    private static void ExigirSinErrores(XElement resultado)
    {
        var errores = LeerMensajes(resultado.Element(Fe + "Errors"), "Err").ToList();
        if (errores.Count > 0)
            throw new FacturacionException(
                $"ARCA respondió con un error: {string.Join(" | ", errores.Select(e => $"{e.Codigo}: {e.Mensaje}"))}",
                reintentable: true);
    }

    private static IEnumerable<(int Codigo, string Mensaje)> LeerMensajes(XElement? lista, string nombre)
        => lista?.Elements(Fe + nombre).Select(e => (
               int.TryParse(e.Element(Fe + "Code")?.Value, out var codigo) ? codigo : 0,
               e.Element(Fe + "Msg")?.Value ?? string.Empty))
           ?? [];

    private static DateOnly? LeerFecha(string? texto)
        => DateOnly.TryParseExact(texto, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;

    // ARCA usa punto decimal y 2 decimales
    private static string Importe(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);
}
