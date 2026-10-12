using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class UsuarioEdicionVentana : Window
{
    public UsuarioEdicionVentana(UsuarioEdicionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // El ViewModel decide cuándo cerrar; la ventana solo obedece
        viewModel.Cerrar += guardado => DialogResult = guardado;

        // Mientras se guarda no se puede cerrar con la X
        Closing += (_, e) => e.Cancel = viewModel.Guardando;

        // Excepción aceptada a MVVM: el PasswordBox de WPF no permite Binding
        PwdContrasena.PasswordChanged += (_, _) => viewModel.Contrasena = PwdContrasena.Password;
        PwdConfirmar.PasswordChanged += (_, _) => viewModel.ConfirmarContrasena = PwdConfirmar.Password;

        viewModel.ContrasenaRechazada += () =>
        {
            PwdContrasena.Clear();
            PwdConfirmar.Clear();
            PwdContrasena.Focus();
        };

        // Al crear, el cursor va al usuario; al editar (usuario bloqueado), al nombre completo
        Loaded += (_, _) =>
        {
            if (viewModel.EsNuevo)
                TxtUsuario.Focus();
            else
                TxtNombreCompleto.Focus();
        };
    }
}
