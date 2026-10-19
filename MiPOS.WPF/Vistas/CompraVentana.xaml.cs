using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CompraVentana : Window
{
    public CompraVentana(CompraViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += registrada => DialogResult = registrada;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Al abrir, el cursor va al proveedor; después de cada acción, al campo de código
        Loaded += (_, _) => CmbProveedor.Focus();
        viewModel.AccionTerminada += () => TxtCodigo.Focus();

        GrillaItems.SelectionChanged += (_, _) =>
        {
            if (GrillaItems.SelectedItem is not null)
                GrillaItems.ScrollIntoView(GrillaItems.SelectedItem);
        };
    }
}
