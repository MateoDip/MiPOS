using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class ProveedorEdicionVentana : Window
{
    public ProveedorEdicionVentana(ProveedorEdicionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += guardado => DialogResult = guardado;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        Loaded += (_, _) => TxtNombre.Focus();
    }
}
