using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Hardware;
using MiPOS.Hardware.Cajon;
using MiPOS.Hardware.Impresion;
using MiPOS.Hardware.Lector;
using MiPOS.Hardware.Terminal;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Negocio.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Ctrl+H: muestra la configuración de los dispositivos de esta caja y permite probarlos
/// (ticket de prueba, abrir el cajón, leer un código). La configuración se cambia en appsettings.json.
/// </summary>
public partial class HardwareViewModel : ObservableObject
{
    private readonly IImpresoraTicket _impresora;
    private readonly CajonDinero _cajon;
    private readonly ProductoServicio _productoServicio;
    private readonly DatosComercio _comercio;

    public HardwareViewModel(IImpresoraTicket impresora, CajonDinero cajon, ProductoServicio productoServicio,
                             DatosComercio comercio, OpcionesImpresion impresion, OpcionesCajon opcionesCajon,
                             OpcionesLector lector, OpcionesTerminal terminal)
    {
        _impresora = impresora;
        _cajon = cajon;
        _productoServicio = productoServicio;
        _comercio = comercio;

        TextoImpresora = impresion.Modo == ModoImpresion.Archivo
            ? $"Archivo de texto en la carpeta \"{impresion.CarpetaArchivos}\" (modo de prueba)"
            : $"{(string.IsNullOrWhiteSpace(impresion.Impresora) ? "Impresora predeterminada de Windows" : impresion.Impresora)} · papel {impresion.AnchoPapelMm} mm · {impresion.CaracteresPorLinea} caracteres por línea";

        TextoCajon = opcionesCajon.Modo switch
        {
            ModoCajon.Impresora => $"Conectado a la impresora{(string.IsNullOrWhiteSpace(opcionesCajon.Impresora) ? " de tickets" : $" \"{opcionesCajon.Impresora}\"")} · comando {opcionesCajon.Comando}",
            ModoCajon.Serial => $"Puerto {opcionesCajon.Puerto} ({opcionesCajon.Baudios} baudios) · comando {opcionesCajon.Comando}",
            _ => "Desactivado"
        };

        TextoLector = lector.Modo == ModoLector.Serial
            ? $"Puerto {lector.Puerto} ({lector.Baudios} baudios). Para probarlo, escanee en la caja."
            : "Como teclado (USB). Escanee en el campo de abajo para probarlo.";

        TextoTerminal = terminal.Modo switch
        {
            ModoTerminal.MercadoPagoPoint => $"Mercado Pago Point integrado · terminal {terminal.TerminalId}",
            ModoTerminal.Simulada => "Simulada (para pruebas: aprueba sola, rechaza los montos terminados en ,99)",
            _ => "Manual: se cobra en la terminal y se anota el cupón en el cobro"
        };
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public string TextoImpresora { get; }
    public string TextoCajon { get; }
    public string TextoLector { get; }
    public string TextoTerminal { get; }

    [ObservableProperty]
    private string codigoPrueba = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    [RelayCommand]
    private async Task ImprimirPruebaAsync()
    {
        Limpiar();
        try
        {
            await _impresora.ImprimirAsync(TicketPrueba.Armar(_comercio, _impresora.CaracteresPorLinea), "Prueba de impresión", TicketPrueba.CodigoQr);
            MensajeExito = "Ticket de prueba enviado. Revise que la regla ocupe una sola línea y que el QR se lea con el celular.";
        }
        catch (ImpresionException ex)
        {
            Mensaje = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AbrirCajonAsync()
    {
        Limpiar();
        if (!_cajon.EstaActivo)
        {
            Mensaje = "El cajón está desactivado (Cajon:Modo en appsettings.json).";
            return;
        }

        try
        {
            await _cajon.AbrirAsync();
            MensajeExito = "Se mandó la orden de abrir el cajón. Si no se abrió, pruebe con el comando del pin 5: \"1B 70 01 19 FA\".";
        }
        catch (HardwareException ex)
        {
            Mensaje = ex.Message;
        }
    }

    // Enter en el campo de prueba del lector
    [RelayCommand]
    private async Task ProbarCodigoAsync()
    {
        Limpiar();
        var codigo = CodigoPrueba.Trim();
        CodigoPrueba = string.Empty;

        if (codigo.Length == 0)
            return;

        try
        {
            var producto = await _productoServicio.BuscarPorCodigoAsync(codigo);
            MensajeExito = producto is null
                ? $"Código leído: {codigo} ({codigo.Length} caracteres). No hay un producto con ese código."
                : $"Código leído: {codigo} ({codigo.Length} caracteres) → {producto.Nombre}.";
        }
        catch (Exception)
        {
            MensajeExito = $"Código leído: {codigo} ({codigo.Length} caracteres).";
        }
    }

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();

    private void Limpiar()
    {
        Mensaje = string.Empty;
        MensajeExito = string.Empty;
    }
}
