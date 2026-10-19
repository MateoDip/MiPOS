namespace MiPOS.Hardware.Terminal;

// Resultado de un cobro en la terminal. IdPago es el número de operación (se guarda en la venta).
public record ResultadoTerminal(bool Aprobado, string? IdPago, string Mensaje);

/// <summary>
/// Una terminal de pago integrada: le manda el monto y espera a que el cliente pague.
/// Hay dos implementaciones: TerminalMercadoPagoPoint (la real) y TerminalSimulada (para probar).
/// En Terminal:Modo = Manual no se usa (el cajero anota el cupón).
/// </summary>
public interface ITerminalPago
{
    /// <summary>
    /// Cobra el monto en la terminal. progreso recibe mensajes para mostrar al cajero ("Esperando el pago...").
    /// Si se cancela (Esc), se intenta anular el cobro; si el cliente ya está pagando, se avisa que hay que
    /// cancelarlo en la terminal y se sigue esperando el resultado (nunca se abandona un cobro en curso).
    /// Lanza HardwareException si no se pudo hablar con la terminal.
    /// </summary>
    Task<ResultadoTerminal> CobrarAsync(decimal monto, string referencia, string descripcion,
                                        IProgress<string>? progreso, CancellationToken cancelacion);
}
