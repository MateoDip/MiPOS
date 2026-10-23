namespace MiPOS.Negocio.Licencias;

/// <summary>
/// El archivo de licencia que se quiere instalar no sirve (dañado, de otra PC, vencido),
/// o falta la licencia para algo que la exige. Su mensaje se puede mostrar tal cual en pantalla.
/// </summary>
public class LicenciaInvalidaException : Exception
{
    public LicenciaInvalidaException(string mensaje) : base(mensaje) { }
}
