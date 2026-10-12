namespace MiPOS.Dominio.Entidades;

/// <summary>
/// Una línea de la venta: un producto con su cantidad y su precio.
/// </summary>
public class DetalleVenta
{
    public int Id { get; set; }

    public int VentaId { get; set; }
    public Venta? Venta { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    // Copia del nombre al momento de vender: si después se cambia el nombre
    // del producto, la venta sigue mostrando lo que realmente se vendió
    public string NombreProducto { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    // Precio congelado del carrito
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal { get; set; }
}
