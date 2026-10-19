namespace MiPOS.Facturacion;

public enum ModoFacturacion
{
    // Sin factura electrónica: el ticket sale como "NO VÁLIDO COMO FACTURA"
    Desactivada,

    // Para desarrollar y probar sin certificado ni Internet: inventa CAE de mentira.
    // Los tickets dicen "SIN VALIDEZ FISCAL".
    Simulada,

    // Servidores de prueba de ARCA (homologación). Necesita un certificado de homologación.
    // Los comprobantes no tienen validez fiscal.
    Homologacion,

    // Servidores reales de ARCA: comprobantes con validez fiscal
    Produccion
}

/// <summary>
/// Configuración técnica de la factura electrónica. Se lee de la sección "Facturacion" de appsettings.json.
/// El CUIT del emisor es el del comercio (sección "Comercio").
/// </summary>
public class OpcionesFacturacion
{
    public ModoFacturacion Modo { get; set; } = ModoFacturacion.Desactivada;

    // Punto de venta habilitado en ARCA para "Web Services". Cada caja (PC) tiene que usar
    // uno distinto: así dos cajas nunca piden el mismo número de comprobante.
    public int PuntoVenta { get; set; } = 1;

    // Certificado digital del comercio, tramitado en ARCA:
    // - un archivo .pfx o .p12 (con ContrasenaCertificado si tiene), o
    // - un archivo .crt / .pem con la clave privada aparte en RutaClavePrivada (.key)
    public string RutaCertificado { get; set; } = string.Empty;
    public string ContrasenaCertificado { get; set; } = string.Empty;
    public string RutaClavePrivada { get; set; } = string.Empty;

    // Donde se guarda el permiso de acceso de ARCA (dura 12 horas) y los datos del modo Simulada.
    // Si no es una ruta completa, es relativa a la carpeta del programa.
    public string CarpetaDatos { get; set; } = "Facturacion";

    // Cuánto se espera la respuesta de ARCA antes de dejar la factura pendiente
    public int TiempoEsperaSegundos { get; set; } = 30;

    public bool EstaActiva => Modo != ModoFacturacion.Desactivada;

    // Simulada y Homologacion generan comprobantes sin validez fiscal
    public bool EsDePrueba => Modo is ModoFacturacion.Simulada or ModoFacturacion.Homologacion;

    public string RutaCarpetaDatos => Path.IsPathRooted(CarpetaDatos)
        ? CarpetaDatos
        : Path.Combine(AppContext.BaseDirectory, CarpetaDatos);

    public void Validar()
    {
        if (!Enum.IsDefined(Modo))
            throw new FacturacionException("Configuración de facturación inválida: Facturacion:Modo debe ser Desactivada, Simulada, Homologacion o Produccion.");

        if (!EstaActiva)
            return;

        if (PuntoVenta < 1 || PuntoVenta > 99998)
            throw new FacturacionException("Configuración de facturación inválida: Facturacion:PuntoVenta debe estar entre 1 y 99998.");

        if (TiempoEsperaSegundos < 5 || TiempoEsperaSegundos > 300)
            throw new FacturacionException("Configuración de facturación inválida: Facturacion:TiempoEsperaSegundos debe estar entre 5 y 300.");

        if (Modo is ModoFacturacion.Homologacion or ModoFacturacion.Produccion)
        {
            if (string.IsNullOrWhiteSpace(RutaCertificado) || !File.Exists(RutaCertificado))
                throw new FacturacionException($"No se encontró el certificado de ARCA (Facturacion:RutaCertificado = \"{RutaCertificado}\").");

            var esPfx = RutaCertificado.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
                        || RutaCertificado.EndsWith(".p12", StringComparison.OrdinalIgnoreCase);

            if (!esPfx && (string.IsNullOrWhiteSpace(RutaClavePrivada) || !File.Exists(RutaClavePrivada)))
                throw new FacturacionException($"No se encontró la clave privada del certificado (Facturacion:RutaClavePrivada = \"{RutaClavePrivada}\").");
        }
    }
}
