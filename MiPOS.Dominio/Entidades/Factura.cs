namespace MiPOS.Dominio.Entidades;

/// <summary>
/// Factura electrónica de una venta. Se guarda junto con la venta (Pendiente)
/// y pasa a Autorizada cuando ARCA da el CAE.
/// </summary>
public class Factura
{
    public int Id { get; set; }

    // Una venta tiene como máximo una factura
    public int VentaId { get; set; }
    public Venta? Venta { get; set; }

    public TipoComprobante Tipo { get; set; }

    public int PuntoVenta { get; set; }

    // 0 mientras no se pidió el CAE. Se guarda antes de pedirlo, para poder averiguar
    // después si ARCA lo autorizó aunque no haya llegado la respuesta.
    public long Numero { get; set; }

    public EstadoFactura Estado { get; set; } = EstadoFactura.Pendiente;

    // Fecha del comprobante (la del día en que se pidió el CAE)
    public DateOnly? Fecha { get; set; }

    public string? Cae { get; set; }
    public DateOnly? VencimientoCae { get; set; }

    // Cliente. Consumidor final sin identificar: DocTipo 99 y DocNumero 0.
    // 80 = CUIT, 96 = DNI (códigos de ARCA)
    public int DocTipo { get; set; }
    public long DocNumero { get; set; }
    public string? ReceptorNombre { get; set; }
    public CondicionIva ReceptorCondicionIva { get; set; }

    // Total = ImporteNeto + ImporteExento + ImporteIva (en la factura C: neto = total, sin IVA)
    public decimal ImporteNeto { get; set; }
    public decimal ImporteExento { get; set; }
    public decimal ImporteIva { get; set; }
    public decimal Total { get; set; }

    // Simulada, Homologacion o Produccion: con qué configuración se emitió.
    // Las de Simulada y Homologacion no tienen validez fiscal.
    public string Entorno { get; set; } = string.Empty;

    // Último error o la observación de ARCA
    public string? Mensaje { get; set; }

    public int Intentos { get; set; }

    public DateTime FechaCreacion { get; set; }

    public List<FacturaIva> Alicuotas { get; set; } = new();
}

/// <summary>
/// IVA de una alícuota dentro de una factura (A o B)
/// </summary>
public class FacturaIva
{
    public int Id { get; set; }

    public int FacturaId { get; set; }
    public Factura? Factura { get; set; }

    public AlicuotaIva Alicuota { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal Importe { get; set; }
}
