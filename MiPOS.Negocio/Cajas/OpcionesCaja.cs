namespace MiPOS.Negocio.Cajas;

/// <summary>
/// Qué caja es esta PC. Se lee de la sección "Caja" de appsettings.json.
/// </summary>
public class OpcionesCaja
{
    // Tiene que coincidir con el Numero de una caja activa de la tabla Cajas
    public int Numero { get; set; } = 1;
}
