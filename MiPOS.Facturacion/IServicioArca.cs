namespace MiPOS.Facturacion;

/// <summary>
/// Los pedidos que se le hacen a ARCA para facturar. Hay dos implementaciones:
/// ServicioArcaWs (los servicios web reales, de homologación o producción) y
/// ServicioArcaSimulado (para desarrollar sin certificado ni Internet).
/// Cuál se usa lo decide la configuración (Facturacion:Modo).
/// Todos los métodos lanzan FacturacionException si no se pudo hablar con ARCA.
/// </summary>
public interface IServicioArca
{
    // Último número autorizado para ese punto de venta y tipo de comprobante (0 si no hay ninguno)
    Task<long> ObtenerUltimoNumeroAsync(long cuitEmisor, int puntoVenta, int tipoComprobante, CancellationToken cancelacion = default);

    Task<RespuestaCae> SolicitarCaeAsync(long cuitEmisor, SolicitudCae solicitud, CancellationToken cancelacion = default);

    // Un comprobante ya autorizado, o null si ese número no existe
    Task<ComprobanteConsultado?> ConsultarAsync(long cuitEmisor, int puntoVenta, int tipoComprobante, long numero,
                                                CancellationToken cancelacion = default);
}
