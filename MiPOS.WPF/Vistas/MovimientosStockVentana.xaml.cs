using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class MovimientosStockVentana : Window
{
    public MovimientosStockVentana(MovimientosStockViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += Close;

        Loaded += (_, _) => GrillaMovimientos.Focus();
    }
}
