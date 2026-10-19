using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class ClienteFacturaVentana : Window
{
    public ClienteFacturaVentana(ClienteFacturaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += aceptado => DialogResult = aceptado;

        Loaded += (_, _) =>
        {
            TxtDocumento.Focus();
            TxtDocumento.SelectAll();
        };
    }
}
