using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class ProductoEdicionVentana : Window
{
    public ProductoEdicionVentana(ProductoEdicionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += guardado => DialogResult = guardado;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Al crear, el cursor va al código (para escanearlo); al editar, al nombre
        Loaded += (_, _) =>
        {
            if (viewModel.EsNuevo)
                TxtCodigo.Focus();
            else
                TxtNombre.Focus();
        };
    }
}
