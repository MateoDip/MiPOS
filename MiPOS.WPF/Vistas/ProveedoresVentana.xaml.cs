using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class ProveedoresVentana : Window
{
    public ProveedoresVentana(ProveedoresViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += Close;

        // Al abrir, el cursor queda en la lista para moverse con las flechas
        Loaded += (_, _) => GrillaProveedores.Focus();
    }
}
