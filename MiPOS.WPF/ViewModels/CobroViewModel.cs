using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Facturacion;
using MiPOS.Hardware;
using MiPOS.Hardware.Terminal;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Ventas;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

public partial class CobroViewModel : ObservableObject
{
    private readonly VentaServicio _ventaServicio;
    private readonly FacturacionServicio _facturacionServicio;
    private readonly DialogoServicio _dialogos;
    private readonly OpcionesTerminal _opcionesTerminal;
    private readonly ITerminalPago _terminal;
    private Carrito? _carrito;

    // Terminal integrada: referencia del cobro en curso (se reutiliza si hay que reintentar, para no cobrar dos veces)
    // y el número de operación cuando la terminal ya aprobó el pago
    private string? _referenciaCobro;
    private string? _pagoAprobado;
    private CancellationTokenSource? _cancelacionTerminal;

    public CobroViewModel(VentaServicio ventaServicio, FacturacionServicio facturacionServicio, DialogoServicio dialogos,
                          OpcionesTerminal opcionesTerminal, ITerminalPago terminal)
    {
        _ventaServicio = ventaServicio;
        _facturacionServicio = facturacionServicio;
        _dialogos = dialogos;
        _opcionesTerminal = opcionesTerminal;
        _terminal = terminal;
    }

    // true = venta registrada, false = cerró sin cobrar. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public ObservableCollection<MedioPago> MediosPago { get; } = new();

    public decimal Total { get; private set; }

    // La venta guardada (solo si se cobró)
    public Venta? VentaRegistrada { get; private set; }

    // true mientras se guarda la venta: no se puede cerrar la ventana
    public bool Guardando { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsEfectivo))]
    [NotifyPropertyChangedFor(nameof(PideCupon))]
    private MedioPago? medioSeleccionado;

    public bool EsEfectivo => MedioSeleccionado?.EsEfectivo == true;

    // Terminal manual (posnet no conectado): se anota el cupón o número de autorización
    public bool PideCupon => MedioSeleccionado?.UsaTerminal == true && !_opcionesTerminal.EsIntegrada;

    [ObservableProperty]
    private string cupon = string.Empty;

    // Terminal integrada: qué está pasando en la terminal ("El cliente está pagando...")
    [ObservableProperty]
    private string mensajeTerminal = string.Empty;

    [ObservableProperty]
    private string pagaCon = string.Empty;

    // A quién se factura (por defecto, consumidor final). Se cambia con F2.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoComprobante))]
    private DatosReceptor receptor = DatosReceptor.ConsumidorFinal;

    public bool FacturacionActiva => _facturacionServicio.EstaActiva;

    // Ej: "Factura B · Consumidor final"
    public string TextoComprobante => FacturacionActiva ? _facturacionServicio.Describir(Receptor) : string.Empty;

    // Texto que se muestra debajo de "Paga con": el vuelto o lo que falta
    [ObservableProperty]
    private string textoVuelto = string.Empty;

    [ObservableProperty]
    private string mensaje = "Cargando medios de pago...";

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(Carrito carrito)
    {
        _carrito = carrito;
        Total = carrito.Total;
        _ = CargarMediosPagoAsync();
    }

    private async Task CargarMediosPagoAsync()
    {
        try
        {
            foreach (var medio in await _ventaServicio.ObtenerMediosPagoAsync())
                MediosPago.Add(medio);

            // Por defecto queda elegido el efectivo
            MedioSeleccionado = MediosPago.FirstOrDefault(m => m.EsEfectivo) ?? MediosPago.FirstOrDefault();
            Mensaje = MediosPago.Count == 0 ? "No hay medios de pago activos." : string.Empty;
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar los medios de pago. Verifique la conexión con la base de datos.";
        }
    }

    partial void OnMedioSeleccionadoChanged(MedioPago? value) => ActualizarVuelto();

    partial void OnPagaConChanged(string value) => ActualizarVuelto();

    private void ActualizarVuelto()
    {
        if (!EsEfectivo || !IntentarLeerMonto(PagaCon, out var monto))
        {
            TextoVuelto = string.Empty;
            return;
        }

        TextoVuelto = Cobro.AlcanzaElPago(Total, monto)
            ? $"Vuelto: $ {Cobro.CalcularVuelto(Total, monto):N2}"
            : $"Falta: $ {Total - monto:N2}";
    }

    // El monto que entrega el cliente tiene que ser mayor a cero
    private static bool IntentarLeerMonto(string texto, out decimal monto)
        => LectorNumeros.IntentarLeerDecimal(texto, out monto) && monto > 0;

    // Flecha arriba
    [RelayCommand]
    private void Subir() => MoverSeleccion(-1);

    // Flecha abajo
    [RelayCommand]
    private void Bajar() => MoverSeleccion(+1);

    private void MoverSeleccion(int paso)
    {
        // Con el pago ya aprobado en la terminal no se puede cambiar el medio
        if (MediosPago.Count == 0 || Guardando || _pagoAprobado is not null)
            return;

        var posicion = MedioSeleccionado is null ? -1 : MediosPago.IndexOf(MedioSeleccionado);
        posicion = Math.Clamp(posicion + paso, 0, MediosPago.Count - 1);
        MedioSeleccionado = MediosPago[posicion];
    }

    // F2: identificar al cliente (DNI o CUIT), por ejemplo para una Factura A
    [RelayCommand]
    private void IdentificarCliente()
    {
        if (!FacturacionActiva || Guardando)
            return;

        if (_dialogos.PedirReceptor(Receptor) is { } elegido)
        {
            Receptor = elegido;
            Mensaje = string.Empty;
        }
    }

    // Enter: registrar la venta. Mientras se guarda, otro Enter no hace nada
    // (el Toolkit no deja ejecutar el comando dos veces al mismo tiempo).
    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (_carrito is null || MedioSeleccionado is null)
            return;

        decimal? montoRecibido = null;

        if (EsEfectivo)
        {
            if (string.IsNullOrWhiteSpace(PagaCon))
            {
                Mensaje = "Ingrese con cuánto paga el cliente.";
                return;
            }

            if (!IntentarLeerMonto(PagaCon, out var monto))
            {
                Mensaje = "Monto inválido: ingrese solo números, con coma para los centavos (ej: 1500,50).";
                return;
            }

            montoRecibido = monto;
        }

        try
        {
            Guardando = true;

            // Terminal integrada: primero se cobra; la venta se registra solo si el pago se aprobó
            var referenciaPago = PideCupon ? Cupon : null;
            if (MedioSeleccionado.UsaTerminal && _opcionesTerminal.EsIntegrada)
            {
                if (_pagoAprobado is null && !await CobrarEnTerminalAsync())
                    return;

                referenciaPago = _pagoAprobado;
            }

            Mensaje = "Registrando venta...";
            VentaRegistrada = await _ventaServicio.RegistrarAsync(_carrito, MedioSeleccionado.Id, montoRecibido,
                FacturacionActiva ? Receptor : null, referenciaPago);
        }
        catch (Exception ex) when (ex is VentaInvalidaException or FacturaInvalidaException or FacturacionException)
        {
            Mensaje = MensajeConPagoCobrado(ex.Message);
        }
        catch (Exception)
        {
            Mensaje = MensajeConPagoCobrado("Error al registrar la venta. No se guardó. Verifique la conexión con la base de datos.");
        }
        finally
        {
            Guardando = false;
        }

        // Se cierra recién cuando terminó de guardar (si no, la ventana no se dejaría cerrar)
        if (VentaRegistrada is not null)
            Cerrar?.Invoke(true);
    }

    // Manda el monto a la terminal y espera. true = pago aprobado.
    private async Task<bool> CobrarEnTerminalAsync()
    {
        // La misma referencia en los reintentos: si el pedido anterior llegó a Mercado Pago, no se crea otro cobro
        _referenciaCobro ??= $"mipos-{Guid.NewGuid():N}";
        _cancelacionTerminal = new CancellationTokenSource();
        var progreso = new Progress<string>(texto => MensajeTerminal = texto);

        try
        {
            Mensaje = string.Empty;
            var resultado = await _terminal.CobrarAsync(Total, _referenciaCobro, "Venta MiPOS", progreso, _cancelacionTerminal.Token);

            if (!resultado.Aprobado)
            {
                MensajeTerminal = string.Empty;
                Mensaje = resultado.Mensaje;
                _referenciaCobro = null; // el próximo intento es un cobro nuevo
                return false;
            }

            _pagoAprobado = resultado.IdPago ?? _referenciaCobro;
            MensajeTerminal = $"Pago aprobado (operación {_pagoAprobado}).";
            return true;
        }
        catch (HardwareException ex)
        {
            MensajeTerminal = string.Empty;
            Mensaje = $"{ex.Message} Puede reintentar con Enter o elegir otro medio de pago.";
            return false;
        }
        finally
        {
            _cancelacionTerminal.Dispose();
            _cancelacionTerminal = null;
        }
    }

    // Si la terminal ya cobró y falló el registro, hay que dejarlo bien claro: no se debe volver a cobrar
    private string MensajeConPagoCobrado(string error)
        => _pagoAprobado is null
            ? error
            : $"ATENCIÓN: el pago YA SE COBRÓ en la terminal (operación {_pagoAprobado}), pero la venta no se registró: {error} Presione Enter para reintentar el registro (no se vuelve a cobrar).";

    // Esc: volver a la caja sin cobrar. Mientras la terminal está cobrando, pide cancelar el cobro.
    [RelayCommand]
    private void Cancelar()
    {
        if (_cancelacionTerminal is not null)
        {
            _cancelacionTerminal.Cancel();
            return;
        }

        if (Guardando)
            return;

        if (_pagoAprobado is not null
            && !_dialogos.Confirmar($"El pago ya se cobró en la terminal (operación {_pagoAprobado}) y la venta NO está registrada. Si sale, tendrá que devolverle el dinero al cliente desde la terminal. ¿Salir igual?",
                "Pago cobrado sin venta"))
            return;

        Cerrar?.Invoke(false);
    }
}
