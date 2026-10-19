namespace MiPOS.Negocio.Compras;

/// <summary>
/// Una línea de la compra mientras se carga en pantalla (todavía no está guardada).
/// </summary>
public record ItemCompra(int ProductoId, string CodigoBarras, string NombreProducto, int Cantidad, decimal CostoUnitario)
{
    public decimal Subtotal => Cantidad * CostoUnitario;
}
