using System.Windows;
using System.Windows.Input;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CajaVentana : Window
{
    public CajaVentana(CajaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Al abrir, el cursor queda listo en el campo de código
        // y se consulta si la caja está abierta (si no, se ofrece abrirla)
        Loaded += (_, _) =>
        {
            TxtCodigo.Focus();
            viewModel.CargarCajaCommand.Execute(null);
        };

        // Después de cada acción (F1, F4, F5, ESC) el cursor vuelve al campo de código
        viewModel.AccionTerminada += () => TxtCodigo.Focus();

        // Al cerrar sesión se cierra la caja (App vuelve a mostrar el inicio de sesión)
        viewModel.SesionCerrada += Close;

        // Al cerrarse, se libera el puerto del lector de códigos (si usa uno)
        Closed += (_, _) => viewModel.LiberarDispositivos();

        // Si el producto seleccionado queda fuera de lo visible, se desplaza la grilla para mostrarlo
        GrillaCarrito.SelectionChanged += (_, _) =>
        {
            if (GrillaCarrito.SelectedItem is not null)
                GrillaCarrito.ScrollIntoView(GrillaCarrito.SelectedItem);
        };

        // F10: Windows la usa para activar el menú de la ventana, así que no llega como tecla
        // normal y no sirve un KeyBinding. Se detecta acá y se ejecuta el comando del ViewModel.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.F10)
            {
                e.Handled = true;
                if (viewModel.CambiarMiContrasenaCommand.CanExecute(null))
                    viewModel.CambiarMiContrasenaCommand.Execute(null);
            }
        };
    }
}