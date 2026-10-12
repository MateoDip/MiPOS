using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Ventas;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

public partial class CobroViewModel : ObservableObject
{
    private readonly VentaServicio _ventaServicio;
    private Carrito? _carrito;

    public CobroViewModel(VentaServicio ventaServicio)
    {
        _ventaServicio = ventaServicio;
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
    private MedioPago? medioSeleccionado;

    public bool EsEfectivo => MedioSeleccionado?.EsEfectivo == true;

    [ObservableProperty]
    private string pagaCon = string.Empty;

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
        if (MediosPago.Count == 0 || Guardando)
            return;

        var posicion = MedioSeleccionado is null ? -1 : MediosPago.IndexOf(MedioSeleccionado);
        posicion = Math.Clamp(posicion + paso, 0, MediosPago.Count - 1);
        MedioSeleccionado = MediosPago[posicion];
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
            Mensaje = "Registrando venta...";
            VentaRegistrada = await _ventaServicio.RegistrarAsync(_carrito, MedioSeleccionado.Id, montoRecibido);
        }
        catch (VentaInvalidaException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al registrar la venta. No se guardó. Verifique la conexión con la base de datos.";
        }
        finally
        {
            Guardando = false;
        }

        // Se cierra recién cuando terminó de guardar (si no, la ventana no se dejaría cerrar)
        if (VentaRegistrada is not null)
            Cerrar?.Invoke(true);
    }

    // Esc: volver a la caja sin cobrar (no se permite mientras se guarda)
    [RelayCommand]
    private void Cancelar()
    {
        if (!Guardando)
            Cerrar?.Invoke(false);
    }
}
