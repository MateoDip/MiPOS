using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Productos;

/// <summary>
/// Lo que se carga en la pantalla de alta o edición de un producto.
/// El stock no está acá: solo cambia con movimientos de stock (ajuste, venta o compra).
/// </summary>
public record DatosProducto(string CodigoBarras, string Nombre, int CategoriaId, decimal Precio, int StockMinimo,
                            AlicuotaIva AlicuotaIva = AlicuotaIva.Iva21);
