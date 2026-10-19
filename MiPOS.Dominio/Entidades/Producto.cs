namespace MiPOS.Dominio.Entidades;

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    // Precio final al público (con IVA incluido)
    public decimal Precio { get; set; }

    // IVA del producto, para la factura electrónica (la mayoría: 21%)
    public AlicuotaIva AlicuotaIva { get; set; } = AlicuotaIva.Iva21;
    public bool Activo { get; set; } = true;

    // Unidades disponibles. Puede quedar negativo si se vende algo que
    // el sistema no tenía cargado (se vende igual y aparece en las alertas).
    // Solo se modifica a través de un MovimientoStock.
    public int Stock { get; set; }

    // Cuando el stock llega a este valor (o menos) el producto aparece en las alertas.
    // 0 = sin alerta por mínimo (igual aparece si el stock queda negativo).
    public int StockMinimo { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
}