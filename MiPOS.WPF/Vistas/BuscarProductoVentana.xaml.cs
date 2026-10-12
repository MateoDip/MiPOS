using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class BuscarProductoVentana : Window
{
    public BuscarProductoVentana(BuscarProductoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += elegido => DialogResult = elegido;

        // Al abrir, el cursor queda listo en el campo de búsqueda
        Loaded += (_, _) => TxtBusqueda.Focus();

        // Si la selección baja más allá de lo visible, se desplaza la lista para mostrarla
        LstResultados.SelectionChanged += (_, _) =>
        {
            if (LstResultados.SelectedItem is not null)
                LstResultados.ScrollIntoView(LstResultados.SelectedItem);
        };
    }
}
