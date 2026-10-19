using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class AjusteStockVentana : Window
{
    public AjusteStockVentana(AjusteStockViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += ajustado => DialogResult = ajustado;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Al abrir, el cursor queda en la cantidad
        Loaded += (_, _) => TxtCantidad.Focus();
    }
}
