namespace MiPOS.Seguridad;

/// <summary>
/// El usuario intentó hacer algo para lo que no tiene permiso.
/// Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class PermisoDenegadoException : Exception
{
    public PermisoDenegadoException(string mensaje) : base(mensaje) { }
}
