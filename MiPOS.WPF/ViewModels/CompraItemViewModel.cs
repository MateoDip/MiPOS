using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Cantidad y costo unitario de un producto de la compra.
/// </summary>
public partial class CompraItemViewModel : ObservableObject
{
    // true = aceptó, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public string NombreProducto { get; private set; } = string.Empty;

    // Los valores leídos (solo si aceptó)
    public int CantidadElegida { get; private set; }
    public decimal CostoElegido { get; private set; }

    [ObservableProperty]
    private string cantidad = string.Empty;

    [ObservableProperty]
    private string costo = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(string nombreProducto, int? cantidadActual, decimal? costoSugerido)
    {
        NombreProducto = nombreProducto;
        Cantidad = cantidadActual?.ToString() ?? string.Empty;
        Costo = costoSugerido?.ToString("0.##") ?? string.Empty;
        Mensaje = costoSugerido is null || cantidadActual is not null ? string.Empty : "El costo propuesto es el de la última compra.";
    }

    // Enter
    [RelayCommand]
    private void Aceptar()
    {
        if (!LectorNumeros.IntentarLeerEntero(Cantidad, out var unidades) || unidades == 0)
        {
            Mensaje = "Cantidad inválida: ingrese un número entero mayor a cero.";
            return;
        }

        if (!LectorNumeros.IntentarLeerDecimal(Costo, out var importe))
        {
            Mensaje = "Costo inválido: ingrese el costo por unidad, con coma para los centavos (ej: 650,50). Puede ser 0 si vino bonificado.";
            return;
        }

        CantidadElegida = unidades;
        CostoElegido = importe;
        Cerrar?.Invoke(true);
    }

    // Esc
    [RelayCommand]
    private void Cancelar() => Cerrar?.Invoke(false);
}
