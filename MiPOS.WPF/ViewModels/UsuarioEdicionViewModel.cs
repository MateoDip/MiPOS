using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Usuarios;
using MiPOS.Seguridad;
using MiPOS.WPF.Idiomas;

namespace MiPOS.WPF.ViewModels;

// Una opción de la lista de idiomas: código ("" = el de la caja) y nombre
public record OpcionIdioma(string Codigo, string Nombre);

/// <summary>
/// Alta de un usuario nuevo o edición de uno existente (nombre completo y rol).
/// El nombre de usuario no se puede cambiar una vez creado.
/// </summary>
public partial class UsuarioEdicionViewModel : ObservableObject
{
    private readonly UsuarioServicio _usuarioServicio;

    // null = usuario nuevo
    private int? _id;

    public UsuarioEdicionViewModel(UsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    // true = guardado, false = cancelado. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Avisa a la ventana que borre los campos de contraseña
    public event Action? ContrasenaRechazada;

    public bool EsNuevo => _id is null;
    public string Titulo => EsNuevo ? "Nuevo usuario" : "Editar usuario";

    public IReadOnlyList<Rol> Roles { get; } = Enum.GetValues<Rol>();

    // Las completa la ventana: el PasswordBox de WPF no permite Binding
    public string Contrasena { get; set; } = string.Empty;
    public string ConfirmarContrasena { get; set; } = string.Empty;

    public bool Guardando { get; private set; }

    [ObservableProperty]
    private string nombreUsuario = string.Empty;

    [ObservableProperty]
    private string nombreCompleto = string.Empty;

    [ObservableProperty]
    private Rol rolSeleccionado = Rol.Cajero;

    // Idioma de las pantallas para este usuario. Código vacío = el de la caja.
    public IReadOnlyList<OpcionIdioma> Idiomas { get; } =
        new[] { new OpcionIdioma(string.Empty, Traductor.Traducir("Idioma de la caja (predeterminado)")) }
            .Concat(Traductor.Disponibles.Select(i => new OpcionIdioma(i.Codigo, i.Nombre)))
            .ToList();

    [ObservableProperty]
    private string idiomaSeleccionado = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void IniciarNuevo()
    {
        _id = null;
        Mensaje = $"La contraseña debe tener al menos {UsuarioServicio.LargoMinimoContrasena} caracteres.";
    }

    public void IniciarEdicion(Usuario usuario)
    {
        _id = usuario.Id;
        NombreUsuario = usuario.NombreUsuario;
        NombreCompleto = usuario.NombreCompleto;
        RolSeleccionado = usuario.Rol;
        IdiomaSeleccionado = usuario.Idioma ?? string.Empty;
    }

    // Enter
    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (EsNuevo && Contrasena != ConfirmarContrasena)
        {
            Mensaje = "Las contraseñas no coinciden.";
            ContrasenaRechazada?.Invoke();
            return;
        }

        var guardado = false;
        try
        {
            Guardando = true;
            Mensaje = "Guardando...";

            var idioma = IdiomaSeleccionado.Length == 0 ? null : IdiomaSeleccionado;
            if (_id is int id)
                await _usuarioServicio.ModificarAsync(id, NombreCompleto, RolSeleccionado, idioma);
            else
                await _usuarioServicio.CrearAsync(NombreUsuario, NombreCompleto, Contrasena, RolSeleccionado, idioma);

            guardado = true;
        }
        catch (Exception ex) when (ex is UsuarioInvalidoException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
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
