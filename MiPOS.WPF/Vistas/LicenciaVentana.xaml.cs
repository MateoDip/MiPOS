using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class LicenciaVentana : Window
{
    public LicenciaVentana(LicenciaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += Close;

        Loaded += (_, _) => TxtCodigo.Focus();
    }
}
