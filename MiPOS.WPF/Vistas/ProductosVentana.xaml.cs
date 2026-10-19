using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class ProductosVentana : Window
{
    public ProductosVentana(ProductosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += Close;

        // Al abrir, el cursor queda en la búsqueda
        Loaded += (_, _) => TxtBusqueda.Focus();

        // Si el producto seleccionado queda fuera de lo visible, se desplaza la grilla para mostrarlo
        GrillaProductos.SelectionChanged += (_, _) =>
        {
            if (GrillaProductos.SelectedItem is not null)
                GrillaProductos.ScrollIntoView(GrillaProductos.SelectedItem);
        };
    }
}
