using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using static MiPOS.Negocio.Comprobantes.FormatoTicket;

namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Arma el texto del comprobante de cierre de caja (arqueo), línea por línea.
/// No sabe nada de impresoras: solo devuelve las líneas.
/// </summary>
public static class TicketCierreCaja
{
    public static IReadOnlyList<string> Armar(ResumenTurno resumen, DatosComercio comercio, int ancho)
    {
        var turno = resumen.Turno;
        var lineas = new List<string>();
        var separador = new string('-', ancho);

        EncabezadoComercio(lineas, comercio, ancho);
        Centrar(lineas, "CIERRE DE CAJA", ancho);

        lineas.Add(separador);
        DosColumnas(lineas, turno.Caja?.Nombre ?? string.Empty, $"Turno N° {turno.Id}", ancho);
        DosColumnas(lineas, "Apertura", Fecha(turno.FechaApertura), ancho);
        AjustarTexto(lineas, $"Por: {turno.UsuarioApertura?.NombreCompleto}", ancho);
        if (turno.FechaCierre is DateTime fechaCierre)
        {
            DosColumnas(lineas, "Cierre", Fecha(fechaCierre), ancho);
            AjustarTexto(lineas, $"Por: {turno.UsuarioCierre?.NombreCompleto}", ancho);
        }

        // Ventas por medio de pago
        lineas.Add(separador);
        DosColumnas(lineas, $"Ventas ({resumen.CantidadVentas})", Dinero(resumen.TotalVentas), ancho);
        foreach (var medio in resumen.VentasPorMedio)
            DosColumnas(lineas, $"{medio.Medio} ({medio.Cantidad})", Dinero(medio.Total), ancho);
        if (resumen.TotalDescuentos > 0)
            DosColumnas(lineas, "Descuentos otorgados", Dinero(resumen.TotalDescuentos), ancho);

        // Arqueo del efectivo
        lineas.Add(separador);
        Centrar(lineas, "EFECTIVO", ancho);
        DosColumnas(lineas, "Inicial", Dinero(turno.MontoInicial), ancho);
        DosColumnas(lineas, "Ventas en efectivo", Dinero(resumen.VentasEfectivo), ancho);
        DosColumnas(lineas, "Ingresos", Dinero(resumen.Ingresos), ancho);
        DosColumnas(lineas, "Retiros", $"-{Dinero(resumen.Retiros)}", ancho);
        DosColumnas(lineas, "Esperado", Dinero(turno.EfectivoEsperado ?? resumen.EfectivoEsperado), ancho);
        if (turno.EfectivoContado is decimal contado)
            DosColumnas(lineas, "Contado", Dinero(contado), ancho);
        if (turno.Diferencia is decimal diferencia)
            DosColumnas(lineas, "DIFERENCIA", DineroConSigno(diferencia), ancho);

        // Detalle de cada retiro e ingreso
        if (turno.Movimientos.Count > 0)
        {
            lineas.Add(separador);
            Centrar(lineas, "MOVIMIENTOS", ancho);
            foreach (var movimiento in turno.Movimientos)
            {
                var signo = movimiento.Tipo == TipoMovimientoCaja.Retiro ? "-" : "+";
                DosColumnas(lineas, $"{movimiento.Fecha:HH:mm} {movimiento.Tipo}", signo + Dinero(movimiento.Monto), ancho);
                AjustarTexto(lineas, movimiento.Motivo, ancho);
            }
        }

        lineas.Add(separador);
        Centrar(lineas, "Firma: ____________________", ancho);

        return lineas;
    }

    private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", Argentina);
}
