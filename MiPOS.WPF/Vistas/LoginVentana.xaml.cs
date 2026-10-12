using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class LoginVentana : Window
{
    public LoginVentana(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += ok => DialogResult = ok;

        // Excepción aceptada a MVVM: el PasswordBox de WPF no permite Binding,
        // así que la ventana le pasa la contraseña al ViewModel cada vez que cambia
        PwdContrasena.PasswordChanged += (_, _) => viewModel.Contrasena = PwdContrasena.Password;
        PwdConfirmar.PasswordChanged += (_, _) => viewModel.ConfirmarContrasena = PwdConfirmar.Password;

        // Contraseña incorrecta: se borran los campos y el cursor vuelve a la contraseña
        viewModel.ContrasenaRechazada += () =>
        {
            PwdContrasena.Clear();
            PwdConfirmar.Clear();
            PwdContrasena.Focus();
        };

        Loaded += (_, _) => TxtUsuario.Focus();
    }
}
