using System.Windows;
using System.Windows.Input;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CobroVentana : Window
{
    public CobroVentana(CobroViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += cobrado => DialogResult = cobrado;

        // Mientras se guarda la venta no se puede cerrar con la X
        // (si no, la venta podría quedar guardada sin que la caja se entere)
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Si se elige efectivo, el cursor va a "Paga con".
        // Si se elige otro medio, "Paga con" se oculta y el foco queda en la ventana,
        // para que las flechas, Enter y Esc sigan funcionando.
        TxtPagaCon.IsVisibleChanged += (_, _) => PonerFoco();
        Loaded += (_, _) => PonerFoco();
    }

    private void PonerFoco()
    {
        if (TxtPagaCon.IsVisible)
            TxtPagaCon.Focus();
        else
            Keyboard.Focus(this);
    }
}
