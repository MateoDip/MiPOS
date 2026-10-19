namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Lo que se manda a la impresora: las líneas de texto y, en las facturas autorizadas,
/// el texto del código QR de ARCA (que la impresora dibuja al final).
/// </summary>
public record TicketParaImprimir(IReadOnlyList<string> Lineas, string? CodigoQr);
