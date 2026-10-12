using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Usuarios;
using MiPOS.Seguridad;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Un usuario con permiso (por ejemplo, un administrador) escribe su usuario y contraseña
/// para autorizar una acción que el usuario conectado no puede hacer solo.
/// </summary>
public partial class AutorizacionViewModel : ObservableObject
{
    private readonly UsuarioServicio _usuarioServicio;
    private Permiso _permiso;

    public AutorizacionViewModel(UsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    // true = autorizado, false = cancelado. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Avisa a la ventana que borre la contraseña y ponga el cursor ahí
    public event Action? ContrasenaRechazada;

    // Qué se está autorizando (ej: "Descuento del 10% sobre $ 5.000,00")
    public string Motivo { get; private set; } = string.Empty;

    // Quién autorizó (solo si se autorizó)
    public Usuario? Autorizante { get; private set; }

    // La completa la ventana: el PasswordBox de WPF no permite Binding
    public string Contrasena { get; set; } = string.Empty;

    // true mientras se verifica: no se puede cancelar
    public bool Verificando { get; private set; }

    [ObservableProperty]
    private string nombreUsuario = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(Permiso permiso, string motivo)
    {
        _permiso = permiso;
        Motivo = motivo;
    }

    // Enter
    [RelayCommand]
    private async Task AutorizarAsync()
    {
        try
        {
            Verificando = true;
            Mensaje = "Verificando...";
            Autorizante = await _usuarioServicio.AutorizarAsync(NombreUsuario, Contrasena, _permiso);
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
        finally
        {
            Verificando = false;
        }

        if (Autorizante is not null)
            Cerrar?.Invoke(true);
    }

    // Esc
    [RelayCommand]
    private void Cancelar()
    {
        if (!Verificando)
            Cerrar?.Invoke(false);
    }
}
