namespace MiPOS.Dominio.Entidades;

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public bool Activo { get; set; } = true;

    // Unidades disponibles. Puede quedar negativo si se vende algo que
    // el sistema no tenía cargado (se vende igual y aparece en las alertas).
    // Solo se modifica a través de un MovimientoStock.
    public int Stock { get; set; }

    // Por debajo de este valor el producto aparece en las alertas (0 = sin alerta)
    public int StockMinimo { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
}