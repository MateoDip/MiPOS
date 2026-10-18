using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Cajas;

// Ventas de un turno con un medio de pago (ej: Débito, 7 ventas, $ 35.000,00)
public record VentasPorMedio(string Medio, bool EsEfectivo, int Cantidad, decimal Total);

/// <summary>
/// Totales de un turno de caja. Es el único lugar donde se calcula cuánto efectivo debería haber.
/// </summary>
public class ResumenTurno
{
    // Viene con Caja, UsuarioApertura, UsuarioCierre y Movimientos cargados
    public required TurnoCaja Turno { get; init; }

    public required IReadOnlyList<VentasPorMedio> VentasPorMedio { get; init; }

    // Suma de los descuentos de todas las ventas del turno
    public required decimal TotalDescuentos { get; init; }

    public int CantidadVentas => VentasPorMedio.Sum(v => v.Cantidad);
    public decimal TotalVentas => VentasPorMedio.Sum(v => v.Total);

    // En efectivo cuenta el total de la venta: lo que entró es "paga con" menos el vuelto
    public decimal VentasEfectivo => VentasPorMedio.Where(v => v.EsEfectivo).Sum(v => v.Total);

    public decimal Ingresos => SumarMovimientos(TipoMovimientoCaja.Ingreso);
    public decimal Retiros => SumarMovimientos(TipoMovimientoCaja.Retiro);

    // Lo que tendría que haber en el cajón
    public decimal EfectivoEsperado => Turno.MontoInicial + VentasEfectivo + Ingresos - Retiros;

    private decimal SumarMovimientos(TipoMovimientoCaja tipo)
        => Turno.Movimientos.Where(m => m.Tipo == tipo).Sum(m => m.Monto);
}
