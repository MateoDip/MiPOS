namespace MiPOS.Hardware.Terminal;

/// <summary>
/// Imita una terminal de pago para probar sin tenerla. Aprueba a los 3 segundos;
/// rechaza los montos terminados en ,99 (para probar el rechazo).
/// </summary>
public class TerminalSimulada : ITerminalPago
{
    public async Task<ResultadoTerminal> CobrarAsync(decimal monto, string referencia, string descripcion,
                                                     IProgress<string>? progreso, CancellationToken cancelacion)
    {
        progreso?.Report($"Terminal simulada: esperando el pago de $ {monto:N2}... (Esc para cancelar)");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancelacion);
        }
        catch (OperationCanceledException)
        {
            return new ResultadoTerminal(false, null, "Cobro cancelado.");
        }

        if (monto % 1 == 0.99m)
            return new ResultadoTerminal(false, null, "Pago rechazado por la terminal (simulado).");

        return new ResultadoTerminal(true, $"SIM-{Random.Shared.Next(100000, 999999)}", "Pago aprobado (simulado).");
    }
}
