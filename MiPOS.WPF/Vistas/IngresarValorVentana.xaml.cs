using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class IngresarValorVentana : Window
{
    public IngresarValorVentana(IngresarValorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += aceptado => DialogResult = aceptado;

        // Al abrir, el cursor queda en el campo con el valor seleccionado,
        // así al escribir se reemplaza el valor anterior
        Loaded += (_, _) =>
        {
            TxtValor.Focus();
            TxtValor.SelectAll();
        };
    }
}
