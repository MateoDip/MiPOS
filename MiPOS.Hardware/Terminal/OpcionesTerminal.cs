namespace MiPOS.Hardware.Terminal;

public enum ModoTerminal
{
    // La terminal (posnet) no está conectada al programa: el cajero cobra en ella
    // y anota en el cobro el número de cupón o de autorización. Es lo más común.
    Manual,

    // Mercado Pago Point integrado: el monto se manda solo a la terminal y se espera el resultado
    MercadoPagoPoint,

    // Para desarrollar y probar sin terminal: aprueba después de unos segundos
    // (rechaza los montos terminados en ,99 para poder probar el rechazo)
    Simulada
}

/// <summary>
/// Configuración de la terminal de pago con tarjeta. Sección "Terminal" de appsettings.json.
/// </summary>
public class OpcionesTerminal
{
    public ModoTerminal Modo { get; set; } = ModoTerminal.Manual;

    // Solo Mercado Pago Point: credencial (Access Token) de la cuenta del comercio
    // y el identificador de la terminal (ej: "NEWLAND_N950__N950NCB123456789").
    // El Access Token permite cobrar en nombre del comercio: no compartirlo.
    public string AccessToken { get; set; } = string.Empty;
    public string TerminalId { get; set; } = string.Empty;

    // Cuánto se espera la respuesta de Mercado Pago en cada consulta
    public int TiempoEsperaSegundos { get; set; } = 20;

    public bool EsIntegrada => Modo != ModoTerminal.Manual;

    public void Validar()
    {
        if (!Enum.IsDefined(Modo))
            throw new HardwareException("Configuración de la terminal inválida: Terminal:Modo debe ser Manual, MercadoPagoPoint o Simulada.");

        if (Modo == ModoTerminal.MercadoPagoPoint && (string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(TerminalId)))
            throw new HardwareException("Configuración de la terminal incompleta: falta Terminal:AccessToken o Terminal:TerminalId en appsettings.json.");
    }
}
