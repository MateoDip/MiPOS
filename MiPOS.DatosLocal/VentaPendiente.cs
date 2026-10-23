namespace MiPOS.DatosLocal;

/// <summary>
/// Una venta hecha sin conexión con la base central, esperando para subirse.
/// La venta completa (con sus detalles y su factura pendiente) se guarda como JSON en Datos:
/// así esta tabla no cambia aunque cambie la entidad Venta.
/// </summary>
public class VentaPendiente
{
    // Número local: es el que sale en el ticket ("Venta S/C N° 5") hasta que se sube
    public int Id { get; set; }

    // El mismo de Venta.IdentificadorUnico: la base central lo usa para no cargarla dos veces
    public Guid IdentificadorUnico { get; set; }

    public DateTime Fecha { get; set; }

    public string Datos { get; set; } = string.Empty;

    // Si falló la subida por algo que no es la conexión, se guarda el motivo para revisarlo
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
}
