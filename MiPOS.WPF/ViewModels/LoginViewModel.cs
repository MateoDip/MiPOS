using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Usuarios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Inicio de sesión. Si la base no tiene usuarios (primer arranque),
/// la misma ventana sirve para crear el primer administrador.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly UsuarioServicio _usuarioServicio;

    // Se sabe si es el primer arranque recién después de consultar la base
    private bool _cargado;

    public LoginViewModel(UsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    // true = sesión iniciada. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Avisa a la ventana que borre los campos de contraseña y ponga el cursor ahí
    public event Action? ContrasenaRechazada;

    // Las contraseñas las completa la ventana: el PasswordBox de WPF no permite Binding
    public string Contrasena { get; set; } = string.Empty;
    public string ConfirmarContrasena { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo))]
    [NotifyPropertyChangedFor(nameof(TextoBoton))]
    private bool esPrimerArranque;

    public string Titulo => EsPrimerArranque ? "Crear el primer administrador" : "Iniciar sesión";
    public string TextoBoton => EsPrimerArranque ? "Crear administrador" : "Ingresar";

    [ObservableProperty]
    private string nombreUsuario = string.Empty;

    [ObservableProperty]
    private string nombreCompleto = string.Empty;

    [ObservableProperty]
    private string mensaje = "Conectando con la base de datos...";

    // Lo llama App antes de mostrar la ventana
    public void Iniciar() => _ = CargarAsync();

    private async Task CargarAsync()
    {
        try
        {
            Mensaje = "Conectando con la base de datos...";
            EsPrimerArranque = !await _usuarioServicio.ExisteAlgunUsuarioAsync();
            _cargado = true;
            Mensaje = EsPrimerArranque
                ? "No hay usuarios. Cree el administrador del sistema."
                : string.Empty;
        }
        catch (Exception)
        {
            Mensaje = "No se pudo conectar con la base de datos. Presione Enter para reintentar.";
        }
    }

    // Enter. El Toolkit no deja ejecutarlo dos veces al mismo tiempo.
    [RelayCommand]
    private async Task IngresarAsync()
    {
        // Si falló la conexión al abrir, Enter reintenta
        if (!_cargado)
        {
            await CargarAsync();
            return;
        }

        try
        {
            if (EsPrimerArranque)
            {
                if (Contrasena != ConfirmarContrasena)
                {
                    Mensaje = "Las contraseñas no coinciden.";
                    ContrasenaRechazada?.Invoke();
                    return;
                }

                Mensaje = "Creando administrador...";
                await _usuarioServicio.CrearPrimerAdministradorAsync(NombreUsuario, NombreCompleto, Contrasena);
            }
            else
            {
                Mensaje = "Verificando...";
                await _usuarioServicio.IniciarSesionAsync(NombreUsuario, Contrasena);
            }

            Cerrar?.Invoke(true);
        }
        catch (UsuarioInvalidoException ex)
        {
            Mensaje = ex.Message;
            ContrasenaRechazada?.Invoke();
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
    }
}
