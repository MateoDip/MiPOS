namespace MiPOS.Dominio.Entidades;

/// <summary>
/// Desde que se abre una caja hasta que se cierra. Todas las ventas y los movimientos
/// de efectivo de ese período quedan asociados al turno.
/// El turno es de la caja, no del cajero: si cambia el usuario, se sigue en el mismo turno.
/// </summary>
public class TurnoCaja
{
    public int Id { get; set; }

    public int CajaId { get; set; }
    public Caja? Caja { get; set; }

    // Apertura: quién, cuándo y con cuánto efectivo empezó la caja
    public int UsuarioAperturaId { get; set; }
    public Usuario? UsuarioApertura { get; set; }
    public DateTime FechaApertura { get; set; }
    public decimal MontoInicial { get; set; }

    // Cierre: quedan en null mientras el turno está abierto
    public int? UsuarioCierreId { get; set; }
    public Usuario? UsuarioCierre { get; set; }
    public DateTime? FechaCierre { get; set; }

    // Arqueo: lo que debería haber en efectivo, lo que contó el cajero y la diferencia
    // (Contado - Esperado: negativo = falta plata, positivo = sobra)
    public decimal? EfectivoEsperado { get; set; }
    public decimal? EfectivoContado { get; set; }
    public decimal? Diferencia { get; set; }

    public bool EstaAbierto => FechaCierre is null;

    public List<MovimientoCaja> Movimientos { get; set; } = new();
}
