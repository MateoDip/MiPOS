using System.Windows;
using System.Windows.Input;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CierreCajaVentana : Window
{
    public CierreCajaVentana(CierreCajaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += cerrada => DialogResult = cerrada;

        // Mientras se cierra la caja no se puede cerrar la ventana con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Al abrir, el cursor queda en el efectivo contado.
        // Cuando aparece el resumen, el campo se oculta y el foco pasa a la ventana (para Enter y Esc).
        Loaded += (_, _) => TxtContado.Focus();
        TxtContado.IsVisibleChanged += (_, _) =>
        {
            if (!TxtContado.IsVisible)
                Keyboard.Focus(this);
        };
    }
}
