using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class HardwareVentana : Window
{
    public HardwareVentana(HardwareViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += Close;

        // Al abrir, el cursor queda en el campo de prueba del lector (listo para escanear)
        Loaded += (_, _) => TxtCodigo.Focus();
    }
}
