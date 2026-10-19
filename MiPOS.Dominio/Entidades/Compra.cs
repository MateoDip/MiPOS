namespace MiPOS.Dominio.Entidades;

/// <summary>
/// Mercadería recibida de un proveedor. Al registrarla se suma al stock.
/// </summary>
public class Compra
{
    public int Id { get; set; }

    public DateTime Fecha { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    // Número de la factura o remito del proveedor (opcional)
    public string? NumeroComprobante { get; set; }

    // Suma de los subtotales de los detalles
    public decimal Total { get; set; }

    // Quién la registró
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public List<DetalleCompra> Detalles { get; set; } = new();
}
