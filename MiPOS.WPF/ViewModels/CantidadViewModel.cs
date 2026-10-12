using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Ventanita para escribir la nueva cantidad.
/// Solo devuelve el texto: la validación la hacen CajaViewModel y el Carrito.
/// </summary>
public partial class CantidadViewModel : ObservableObject
{
    // true = aceptó, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    [ObservableProperty]
    private string texto = string.Empty;

    // Enter
    [RelayCommand]
    private void Aceptar() => Cerrar?.Invoke(true);

    // Esc
    [RelayCommand]
    private void Cancelar() => Cerrar?.Invoke(false);
}
