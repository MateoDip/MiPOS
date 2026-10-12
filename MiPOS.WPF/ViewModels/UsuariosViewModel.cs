using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Usuarios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Lista de usuarios (solo administrador): alta, edición, restablecer contraseña, activar/desactivar.
/// </summary>
public partial class UsuariosViewModel : ObservableObject
{
    private readonly UsuarioServicio _usuarioServicio;
    private readonly DialogoServicio _dialogos;

    public UsuariosViewModel(UsuarioServicio usuarioServicio, DialogoServicio dialogos)
    {
        _usuarioServicio = usuarioServicio;
        _dialogos = dialogos;
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public ObservableCollection<Usuario> Usuarios { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoActivar))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestablecerContrasenaCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarActivoCommand))]
    private Usuario? usuarioSeleccionado;

    // El botón dice "Activar" o "Desactivar" según el usuario seleccionado
    public string TextoActivar => UsuarioSeleccionado?.Activo == false ? "Activar" : "Desactivar";

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar() => _ = CargarAsync();

    private async Task CargarAsync(int? seleccionarId = null)
    {
        try
        {
            var usuarios = await _usuarioServicio.ObtenerTodosAsync();

            Usuarios.Clear();
            foreach (var usuario in usuarios)
                Usuarios.Add(usuario);

            UsuarioSeleccionado = Usuarios.FirstOrDefault(u => u.Id == seleccionarId) ?? Usuarios.FirstOrDefault();
            Mensaje = string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar los usuarios. Verifique la conexión con la base de datos.";
        }
    }

    [RelayCommand]
    private async Task NuevoAsync()
    {
        MensajeExito = string.Empty;
        if (_dialogos.EditarUsuario(null))
        {
            await CargarAsync(UsuarioSeleccionado?.Id);
            MensajeExito = "Usuario creado.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayUsuarioSeleccionado))]
    private async Task EditarAsync()
    {
        if (UsuarioSeleccionado is null)
            return;

        MensajeExito = string.Empty;
        var id = UsuarioSeleccionado.Id;
        if (_dialogos.EditarUsuario(UsuarioSeleccionado))
        {
            await CargarAsync(id);
            MensajeExito = "Cambios guardados.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayUsuarioSeleccionado))]
    private void RestablecerContrasena()
    {
        if (UsuarioSeleccionado is null)
            return;

        MensajeExito = string.Empty;
        if (_dialogos.RestablecerContrasena(UsuarioSeleccionado))
            MensajeExito = $"Contraseña restablecida para {UsuarioSeleccionado.NombreUsuario}.";
    }

    [RelayCommand(CanExecute = nameof(HayUsuarioSeleccionado))]
    private async Task CambiarActivoAsync()
    {
        if (UsuarioSeleccionado is null)
            return;

        var usuario = UsuarioSeleccionado;
        var activar = !usuario.Activo;
        var pregunta = activar
            ? $"¿Activar a {usuario.NombreCompleto}? Podrá volver a iniciar sesión."
            : $"¿Desactivar a {usuario.NombreCompleto}? No podrá iniciar sesión.";

        MensajeExito = string.Empty;
        if (!_dialogos.Confirmar(pregunta, activar ? "Activar usuario" : "Desactivar usuario"))
            return;

        try
        {
            await _usuarioServicio.CambiarActivoAsync(usuario.Id, activar);
            await CargarAsync(usuario.Id);
            MensajeExito = activar ? "Usuario activado." : "Usuario desactivado.";
        }
        catch (Exception ex) when (ex is UsuarioInvalidoException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
    }

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();

    private bool HayUsuarioSeleccionado() => UsuarioSeleccionado is not null;
}
