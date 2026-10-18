namespace MiPOS.Dominio.Entidades;

// Los números se guardan en la base: no cambiarlos una vez que haya movimientos
public enum TipoMovimientoCaja
{
    // Entra efectivo que no es una venta (ej: cambio traído del banco)
    Ingreso = 1,

    // Sale efectivo que no es un vuelto (ej: retiro para no acumular plata en el cajón)
    Retiro = 2
}

/// <summary>
/// Entrada o salida de efectivo durante un turno, que no es una venta. Entra en el arqueo.
/// </summary>
public class MovimientoCaja
{
    public int Id { get; set; }

    public int TurnoCajaId { get; set; }
    public TurnoCaja? TurnoCaja { get; set; }

    public TipoMovimientoCaja Tipo { get; set; }

    // Siempre positivo: el Tipo indica si suma o resta
    public decimal Monto { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    // Quién lo registró (el usuario conectado)
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Quién lo autorizó (null si no hizo falta autorización)
    public int? AutorizadoPorId { get; set; }
    public Usuario? AutorizadoPor { get; set; }
}
