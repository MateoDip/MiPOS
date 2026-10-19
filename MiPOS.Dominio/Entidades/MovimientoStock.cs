namespace MiPOS.Dominio.Entidades;

// Los números se guardan en la base: no cambiarlos una vez que haya movimientos
public enum TipoMovimientoStock
{
    // Sale mercadería porque se vendió
    Venta = 1,

    // Entra mercadería comprada a un proveedor
    Compra = 2,

    // Corrección a mano (ej: rotura, vencimiento, diferencia al contar)
    Ajuste = 3
}

/// <summary>
/// Cada cambio en el stock de un producto queda registrado acá,
/// para saber siempre por qué el stock tiene el valor que tiene.
/// </summary>
public class MovimientoStock
{
    public int Id { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public TipoMovimientoStock Tipo { get; set; }

    // Con signo: positivo entra mercadería, negativo sale
    public int Cantidad { get; set; }

    // Obligatorio en los ajustes; en ventas y compras puede quedar vacío
    public string? Motivo { get; set; }

    public DateTime Fecha { get; set; }

    // Quién lo hizo (el usuario conectado)
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // La venta que lo originó (solo en los movimientos de tipo Venta)
    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }
}
