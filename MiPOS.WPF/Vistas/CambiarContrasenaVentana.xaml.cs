using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CambiarContrasenaVentana : Window
{
    public CambiarContrasenaVentana(CambiarContrasenaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += guardado => DialogResult = guardado;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Excepción aceptada a MVVM: el PasswordBox de WPF no permite Binding
        PwdActual.PasswordChanged += (_, _) => viewModel.ContrasenaActual = PwdActual.Password;
        PwdNueva.PasswordChanged += (_, _) => viewModel.ContrasenaNueva = PwdNueva.Password;
        PwdConfirmar.PasswordChanged += (_, _) => viewModel.ConfirmarContrasena = PwdConfirmar.Password;

        // Error: se borran todos los campos y el cursor vuelve al primero
        viewModel.ContrasenaRechazada += () =>
        {
            PwdActual.Clear();
            PwdNueva.Clear();
            PwdConfirmar.Clear();
            PonerFoco(viewModel);
        };

        Loaded += (_, _) => PonerFoco(viewModel);
    }

    private void PonerFoco(CambiarContrasenaViewModel viewModel)
    {
        if (viewModel.PideActual)
            PwdActual.Focus();
        else
            PwdNueva.Focus();
    }
}
