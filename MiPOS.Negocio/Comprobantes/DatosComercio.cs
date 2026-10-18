namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Datos del negocio que se imprimen en el ticket. Cambian en cada instalación
/// (sección "Comercio" de appsettings.json). Los campos vacíos no se imprimen.
/// </summary>
public class DatosComercio
{
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;

    // Mensaje al final del ticket (ej: "¡Gracias por su compra!")
    public string MensajePie { get; set; } = string.Empty;
}
