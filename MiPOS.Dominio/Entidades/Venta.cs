namespace MiPOS.Dominio.Entidades;

public class Venta
{
    // También es el número de venta interno
    public int Id { get; set; }

    // Hora local de la PC al momento de cobrar
    public DateTime Fecha { get; set; }

    // Suma de los productos, antes del descuento
    public decimal Subtotal { get; set; }

    // Descuento sobre el total (0 = sin descuento) y el monto descontado
    public decimal DescuentoPorcentaje { get; set; }
    public decimal Descuento { get; set; }

    // Quién autorizó el descuento (null si no hubo descuento)
    public int? DescuentoAutorizadoPorId { get; set; }
    public Usuario? DescuentoAutorizadoPor { get; set; }

    // Lo que pagó el cliente: Subtotal - Descuento
    public decimal Total { get; set; }

    // Quién hizo la venta (el usuario con la sesión iniciada al cobrar)
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Turno de caja en el que se cobró. Es null solo en las ventas anteriores al control de caja.
    public int? TurnoCajaId { get; set; }
    public TurnoCaja? TurnoCaja { get; set; }

    // Un único medio de pago por venta
    public int MedioPagoId { get; set; }
    public MedioPago? MedioPago { get; set; }

    // Solo en efectivo: con cuánto pagó el cliente y cuánto se le devolvió.
    // En los demás medios quedan en null.
    public decimal? MontoRecibido { get; set; }
    public decimal? Vuelto { get; set; }

    public List<DetalleVenta> Detalles { get; set; } = new();
}
