using MiPOS.Dominio.Entidades;

namespace MiPOS.Seguridad;

/// <summary>
/// Quién está usando el sistema en este momento.
/// Se registra como Singleton: hay una sola sesión compartida por toda la aplicación.
/// </summary>
public class Sesion
{
    public Usuario? UsuarioActual { get; private set; }

    public bool HaySesion => UsuarioActual is not null;

    public void Iniciar(Usuario usuario) => UsuarioActual = usuario;

    public void Cerrar() => UsuarioActual = null;

    public bool Tiene(Permiso permiso)
        => UsuarioActual is not null && Permisos.Tiene(UsuarioActual.Rol, permiso);

    // Devuelve el usuario conectado, o lanza una excepción si no hay sesión
    public Usuario ExigirUsuario()
        => UsuarioActual ?? throw new PermisoDenegadoException("No hay un usuario con sesión iniciada.");

    // Lanza una excepción si el usuario conectado no tiene el permiso
    public void Exigir(Permiso permiso)
    {
        ExigirUsuario();

        if (!Tiene(permiso))
            throw new PermisoDenegadoException("No tiene permiso para realizar esta acción.");
    }
}
