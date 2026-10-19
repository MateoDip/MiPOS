using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CompraItemVentana : Window
{
    public CompraItemVentana(CompraItemViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += aceptado => DialogResult = aceptado;

        // Al abrir, el cursor queda en la cantidad con el texto seleccionado (para reemplazarlo)
        Loaded += (_, _) =>
        {
            TxtCantidad.Focus();
            TxtCantidad.SelectAll();
        };
    }
}
