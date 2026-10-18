using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class MovimientoCajaVentana : Window
{
    public MovimientoCajaVentana(MovimientoCajaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += registrado => DialogResult = registrado;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Al abrir, el cursor queda en el monto
        Loaded += (_, _) => TxtMonto.Focus();
    }
}
