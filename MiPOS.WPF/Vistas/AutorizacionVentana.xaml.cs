using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class AutorizacionVentana : Window
{
    public AutorizacionVentana(AutorizacionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += autorizado => DialogResult = autorizado;

        // Mientras se verifica la contraseña no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Verificando;

        // Excepción aceptada a MVVM: el PasswordBox de WPF no permite Binding
        PwdContrasena.PasswordChanged += (_, _) => viewModel.Contrasena = PwdContrasena.Password;

        viewModel.ContrasenaRechazada += () =>
        {
            PwdContrasena.Clear();
            PwdContrasena.Focus();
        };

        Loaded += (_, _) => TxtUsuario.Focus();
    }
}
