using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CantidadVentana : Window
{
    public CantidadVentana(CantidadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += aceptado => DialogResult = aceptado;

        // Al abrir, el cursor queda en el campo con el número seleccionado,
        // así al escribir se reemplaza la cantidad anterior
        Loaded += (_, _) =>
        {
            TxtCantidad.Focus();
            TxtCantidad.SelectAll();
        };
    }
}
