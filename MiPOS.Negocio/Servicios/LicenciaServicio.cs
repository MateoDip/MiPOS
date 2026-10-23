using MiPOS.Licencias;
using MiPOS.Negocio.Licencias;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

/// <summary>
/// Licencia de esta instalación. Sin licencia válida el sistema funciona en modo demostración:
/// se puede usar todo, pero los tickets dicen "MODO DEMOSTRACIÓN" y no se puede facturar en Produccion.
/// </summary>
public class LicenciaServicio
{
    private readonly OpcionesLicencia _opciones;
    private readonly Sesion _sesion;

    public LicenciaServicio(OpcionesLicencia opciones, Sesion sesion)
    {
        _opciones = opciones;
        _sesion = sesion;
    }

    public string CodigoInstalacion => MiPOS.Licencias.CodigoInstalacion.DeEstaPc;

    // Se lee el archivo cada vez (es chico): así una licencia recién instalada vale enseguida
    public ResultadoLicencia Verificar()
        => ArchivoLicencia.Verificar(_opciones.RutaCompleta, CodigoInstalacion, DateOnly.FromDateTime(DateTime.Today));

    public bool EsValida => Verificar().EsValida;

    /// <summary>
    /// Instala el archivo de licencia elegido (lo copia junto al programa). Si ya hay una licencia válida,
    /// solo la puede reemplazar quien tiene el permiso GestionarLicencia. Lanza LicenciaInvalidaException si no sirve.
    /// </summary>
    public ResultadoLicencia Instalar(string rutaArchivo)
    {
        if (EsValida)
            _sesion.Exigir(Permiso.GestionarLicencia);

        string texto;
        try
        {
            texto = File.ReadAllText(rutaArchivo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LicenciaInvalidaException($"No se pudo leer el archivo: {ex.Message}");
        }

        var resultado = ArchivoLicencia.VerificarTexto(texto, CodigoInstalacion, DateOnly.FromDateTime(DateTime.Today),
            ArchivoLicencia.ClavePublica);

        switch (resultado.Estado)
        {
            case EstadoLicencia.Invalida:
                throw new LicenciaInvalidaException("El archivo no es una licencia válida de MiPOS (está dañado o fue modificado).");
            case EstadoLicencia.OtraInstalacion:
                throw new LicenciaInvalidaException(
                    $"La licencia es para otra PC (código {resultado.Licencia?.CodigoInstalacion}). El código de esta PC es {CodigoInstalacion}.");
            case EstadoLicencia.Vencida:
                throw new LicenciaInvalidaException($"La licencia venció el {resultado.Licencia?.Vencimiento:dd/MM/yyyy}.");
        }

        try
        {
            File.WriteAllText(_opciones.RutaCompleta, texto);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LicenciaInvalidaException(
                $"La licencia es válida pero no se pudo guardar en {_opciones.RutaCompleta}: {ex.Message} Ejecute MiPOS como administrador una vez para instalarla.");
        }

        return resultado;
    }
}
