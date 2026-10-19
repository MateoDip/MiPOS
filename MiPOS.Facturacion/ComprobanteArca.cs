namespace MiPOS.Facturacion;

// Importe de IVA de una alícuota. Id es el código de ARCA (3 = 0%, 4 = 10,5%, 5 = 21%, 6 = 27%, 8 = 5%, 9 = 2,5%).
public record AlicuotaArca(int Id, decimal BaseImponible, decimal Importe);

/// <summary>
/// Datos de un comprobante (factura) para pedirle el CAE a ARCA.
/// Los códigos (tipo de comprobante, documento, condición frente al IVA) son los de ARCA.
/// </summary>
public class SolicitudCae
{
    public int PuntoVenta { get; init; }

    // 1 = Factura A, 6 = Factura B, 11 = Factura C
    public int TipoComprobante { get; init; }

    public long Numero { get; init; }

    public DateOnly Fecha { get; init; }

    // 80 = CUIT, 96 = DNI, 99 = consumidor final sin identificar (DocNumero 0)
    public int DocTipo { get; init; }
    public long DocNumero { get; init; }

    // 1 = Responsable Inscripto, 4 = Exento, 5 = Consumidor Final, 6 = Monotributo
    public int CondicionIvaReceptor { get; init; }

    // ImporteTotal = ImporteNeto + ImporteExento + ImporteIva
    public decimal ImporteTotal { get; init; }
    public decimal ImporteNeto { get; init; }
    public decimal ImporteExento { get; init; }
    public decimal ImporteIva { get; init; }

    // Vacío en la Factura C (no discrimina IVA)
    public IReadOnlyList<AlicuotaArca> Alicuotas { get; init; } = [];
}

/// <summary>
/// Respuesta de ARCA al pedido de CAE. Si no se aprobó, Mensajes dice por qué.
/// </summary>
public record RespuestaCae(bool Aprobado, string? Cae, DateOnly? VencimientoCae, string Mensajes, IReadOnlyList<int> Codigos);

/// <summary>
/// Un comprobante ya autorizado, tal como lo informa ARCA al consultarlo.
/// </summary>
public record ComprobanteConsultado(long Numero, string Cae, DateOnly VencimientoCae, DateOnly Fecha, decimal ImporteTotal, long DocNumero);
