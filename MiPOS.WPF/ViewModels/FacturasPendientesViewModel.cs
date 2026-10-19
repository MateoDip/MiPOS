using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Facturacion;
using MiPOS.Hardware.Impresion;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Ctrl+F: facturas que todavía no tienen CAE (pendientes por falta de conexión, o rechazadas por ARCA).
/// Cualquier usuario puede reintentar; volver a pendiente una rechazada es para un administrador.
/// </summary>
public partial class FacturasPendientesViewModel : ObservableObject
{
    private readonly FacturacionServicio _facturacionServicio;
    private readonly TicketServicio _ticketServicio;
    private readonly IImpresoraTicket _impresora;
    private readonly DialogoServicio _dialogos;
    private readonly Sesion _sesion;

    public FacturasPendientesViewModel(FacturacionServicio facturacionServicio, TicketServicio ticketServicio,
                                       IImpresoraTicket impresora, DialogoServicio dialogos, Sesion sesion)
    {
        _facturacionServicio = facturacionServicio;
        _ticketServicio = ticketServicio;
        _impresora = impresora;
        _dialogos = dialogos;
        _sesion = sesion;
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public ObservableCollection<Factura> Facturas { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReimprimirCommand))]
    [NotifyCanExecuteChangedFor(nameof(VolverAPendienteCommand))]
    private Factura? facturaSeleccionada;

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar() => _ = CargarAsync();

    private async Task CargarAsync()
    {
        try
        {
            var seleccionadaId = FacturaSeleccionada?.Id;
            var facturas = await _facturacionServicio.ObtenerSinAutorizarAsync();

            Facturas.Clear();
            foreach (var factura in facturas)
                Facturas.Add(factura);

            FacturaSeleccionada = Facturas.FirstOrDefault(f => f.Id == seleccionadaId) ?? Facturas.FirstOrDefault();
            if (Facturas.Count == 0 && MensajeExito.Length == 0)
                MensajeExito = "No hay facturas sin autorizar.";
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar las facturas. Verifique la conexión con la base de datos.";
        }
    }

    // Pide el CAE de todas las pendientes de esta caja
    [RelayCommand]
    private async Task ReintentarAsync()
    {
        Mensaje = string.Empty;
        MensajeExito = "Pidiendo los CAE a ARCA...";

        try
        {
            var resultado = await _facturacionServicio.AutorizarPendientesAsync();

            MensajeExito = resultado.Autorizadas == 0 ? string.Empty : $"Facturas autorizadas: {resultado.Autorizadas}. Se pueden reimprimir con F8 o desde acá.";
            if (resultado.UltimoError is not null)
                Mensaje = resultado.SiguenPendientes > 0
                    ? $"Quedan {resultado.SiguenPendientes} pendientes: {resultado.UltimoError}"
                    : resultado.UltimoError;
        }
        catch (Exception ex) when (ex is FacturacionException or FacturaInvalidaException)
        {
            MensajeExito = string.Empty;
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            MensajeExito = string.Empty;
            Mensaje = "Error al reintentar. Verifique la conexión con la base de datos.";
        }

        await CargarAsync();
    }

    // Imprime el ticket de la venta tal como está ahora (si ya se autorizó, sale como factura)
    [RelayCommand(CanExecute = nameof(HayFacturaSeleccionada))]
    private async Task ReimprimirAsync()
    {
        if (FacturaSeleccionada is not { } factura)
            return;

        try
        {
            var ticket = await _ticketServicio.ArmarAsync(factura.VentaId, _impresora.CaracteresPorLinea, esReimpresion: true);
            await _impresora.ImprimirAsync(ticket.Lineas, $"Venta {factura.VentaId}", ticket.CodigoQr);
            MensajeExito = $"Ticket de la venta N° {factura.VentaId} reimpreso.";
        }
        catch (ImpresionException ex)
        {
            Mensaje = $"No se pudo imprimir. {ex.Message}";
        }
        catch (Exception)
        {
            Mensaje = "No se pudo imprimir el ticket.";
        }
    }

    // Una rechazada vuelve a pendiente (por ejemplo, después de corregir la configuración en ARCA)
    [RelayCommand(CanExecute = nameof(PuedeVolverAPendiente))]
    private async Task VolverAPendienteAsync()
    {
        if (FacturaSeleccionada is not { } factura)
            return;

        if (!_dialogos.Confirmar($"¿Volver a pedir el CAE de la factura de la venta N° {factura.VentaId}? Hágalo solo si se corrigió lo que causó el rechazo.",
                "Volver a pendiente"))
            return;

        try
        {
            await _facturacionServicio.VolverAPendienteAsync(factura.Id);
            MensajeExito = "La factura volvió a pendiente. Use \"Reintentar\".";
        }
        catch (Exception ex) when (ex is FacturaInvalidaException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }

        await CargarAsync();
    }

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();

    private bool HayFacturaSeleccionada() => FacturaSeleccionada is not null;

    private bool PuedeVolverAPendiente()
        => FacturaSeleccionada?.Estado == EstadoFactura.Rechazada && _sesion.Tiene(Permiso.ConfigurarFacturacion);
}
