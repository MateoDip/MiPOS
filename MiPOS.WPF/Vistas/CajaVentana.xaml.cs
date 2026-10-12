using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CajaVentana : Window
{
    public CajaVentana(CajaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Al abrir, el cursor queda listo en el campo de código
        Loaded += (_, _) => TxtCodigo.Focus();

        // Después de cada acción (F1, F4, F5, ESC) el cursor vuelve al campo de código
        viewModel.AccionTerminada += () => TxtCodigo.Focus();

        // Si el producto seleccionado queda fuera de lo visible, se desplaza la grilla para mostrarlo
        GrillaCarrito.SelectionChanged += (_, _) =>
        {
            if (GrillaCarrito.SelectedItem is not null)
                GrillaCarrito.ScrollIntoView(GrillaCarrito.SelectedItem);
        };
    }
}