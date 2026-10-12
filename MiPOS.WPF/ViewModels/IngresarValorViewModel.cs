using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Ventanita para escribir un valor (cantidad, porcentaje de descuento, etc.).
/// Solo devuelve el texto: la validación la hacen CajaViewModel y el negocio.
/// </summary>
public partial class IngresarValorViewModel : ObservableObject
{
    // true = aceptó, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Los completa DialogoServicio antes de mostrar la ventana
    public string Titulo { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;

    [ObservableProperty]
    private string texto = string.Empty;

    // Enter
    [RelayCommand]
    private void Aceptar() => Cerrar?.Invoke(true);

    // Esc
    [RelayCommand]
    private void Cancelar() => Cerrar?.Invoke(false);
}
