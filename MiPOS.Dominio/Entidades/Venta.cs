namespace MiPOS.Dominio.Entidades;

public class Venta
{
    // También es el número de venta interno
    public int Id { get; set; }

    // Hora local de la PC al momento de cobrar
    public DateTime Fecha { get; set; }

    public decimal Total { get; set; }

    // Un único medio de pago por venta
    public int MedioPagoId { get; set; }
    public MedioPago? MedioPago { get; set; }

    // Solo en efectivo: con cuánto pagó el cliente y cuánto se le devolvió.
    // En los demás medios quedan en null.
    public decimal? MontoRecibido { get; set; }
    public decimal? Vuelto { get; set; }

    public List<DetalleVenta> Detalles { get; set; } = new();
}
