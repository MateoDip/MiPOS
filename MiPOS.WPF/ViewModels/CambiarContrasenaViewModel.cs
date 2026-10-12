using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Usuarios;
using MiPOS.Seguridad;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Sirve para dos cosas:
/// - Cambiar mi contraseña (F10): pide la contraseña actual.
/// - Restablecer la contraseña de otro usuario (administrador): no pide la actual.
/// </summary>
public partial class CambiarContrasenaViewModel : ObservableObject
{
    private readonly UsuarioServicio _usuarioServicio;

    // null = cambiar la propia; con valor = restablecer la de ese usuario
    private int? _idARestablecer;

    public CambiarContrasenaViewModel(UsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    // true = contraseña cambiada, false = cancelado. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Avisa a la ventana que borre los campos de contraseña
    public event Action? ContrasenaRechazada;

    public string Titulo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool PideActual => _idARestablecer is null;

    // Las completa la ventana: el PasswordBox de WPF no permite Binding
    public string ContrasenaActual { get; set; } = string.Empty;
    public string ContrasenaNueva { get; set; } = string.Empty;
    public string ConfirmarContrasena { get; set; } = string.Empty;

    public bool Guardando { get; private set; }

    [ObservableProperty]
    private string mensaje = $"Mínimo {UsuarioServicio.LargoMinimoContrasena} caracteres.";

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void IniciarCambioPropio()
    {
        _idARestablecer = null;
        Titulo = "Cambiar mi contraseña";
        Descripcion = "Escriba su contraseña actual y la nueva.";
    }

    public void IniciarRestablecer(Usuario usuario)
    {
        _idARestablecer = usuario.Id;
        Titulo = "Restablecer contraseña";
        Descripcion = $"Nueva contraseña para {usuario.NombreCompleto} ({usuario.NombreUsuario}).";
    }

    // Enter
    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (ContrasenaNueva != ConfirmarContrasena)
        {
            Mensaje = "Las contraseñas nuevas no coinciden.";
            ContrasenaRechazada?.Invoke();
            return;
        }

        var guardado = false;
        try
        {
            Guardando = true;
            Mensaje = "Guardando...";

            if (_idARestablecer is int id)
                await _usuarioServicio.RestablecerContrasenaAsync(id, ContrasenaNueva);
            else
                await _usuarioServicio.CambiarMiContrasenaAsync(ContrasenaActual, ContrasenaNueva);

            guardado = true;
        }
        catch (Exception ex) when (ex is UsuarioInvalidoException or PermisoDenegadoException)
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
            Guardando = false;
        }

        if (guardado)
            Cerrar?.Invoke(true);
    }

    // Esc
    [RelayCommand]
    private void Cancelar()
    {
        if (!Guardando)
            Cerrar?.Invoke(false);
    }
}
