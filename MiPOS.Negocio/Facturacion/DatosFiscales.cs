using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Facturacion;

/// <summary>
/// Datos fiscales del comercio para la factura electrónica. Se leen de la sección "Facturacion"
/// de appsettings.json (la misma que OpcionesFacturacion). El CUIT es el de la sección "Comercio".
/// </summary>
public class DatosFiscales
{
    // Monotributo emite factura C; Responsable Inscripto emite A o B
    public CondicionIva CondicionIva { get; set; } = CondicionIva.Monotributo;

    // Nombre legal que figura en ARCA. Vacío = se usa Comercio:Nombre.
    public string RazonSocial { get; set; } = string.Empty;

    // Número de Ingresos Brutos y fecha de inicio de actividades (se imprimen si no están vacíos)
    public string IngresosBrutos { get; set; } = string.Empty;
    public string InicioActividades { get; set; } = string.Empty;

    // Desde este total, ARCA exige identificar al consumidor final (DNI o CUIT).
    // El valor lo fija ARCA y cambia con el tiempo: confirmarlo con el contador.
    public decimal MontoMaximoSinIdentificar { get; set; } = 10_000_000m;
}
