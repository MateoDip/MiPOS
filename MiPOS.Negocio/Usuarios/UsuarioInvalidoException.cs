namespace MiPOS.Negocio.Usuarios;

/// <summary>
/// Error de una regla de usuarios (contraseña incorrecta, nombre repetido, último administrador, etc.).
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class UsuarioInvalidoException : Exception
{
    public UsuarioInvalidoException(string mensaje) : base(mensaje) { }
}
