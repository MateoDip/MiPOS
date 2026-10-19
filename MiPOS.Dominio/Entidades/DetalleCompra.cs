namespace MiPOS.Dominio.Entidades;

/// <summary>
/// Una línea de la compra: un producto con su cantidad y su costo.
/// </summary>
public class DetalleCompra
{
    public int Id { get; set; }

    public int CompraId { get; set; }
    public Compra? Compra { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    // Copia del nombre al momento de comprar (igual que en DetalleVenta)
    public string NombreProducto { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    // Lo que cobró el proveedor por unidad (puede ser 0 si vino bonificado)
    public decimal CostoUnitario { get; set; }

    public decimal Subtotal { get; set; }
}
