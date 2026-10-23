using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Negocio.Licencias;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Licencia de esta PC: muestra el código de instalación (para pedir la licencia) y permite instalar
/// el archivo recibido. Se abre sola al arrancar si no hay licencia válida, y desde la caja con Ctrl+L.
/// </summary>
public partial class LicenciaViewModel : ObservableObject
{
    private readonly LicenciaServicio _licenciaServicio;
    private readonly DialogoServicio _dialogos;
    private readonly Sesion _sesion;

    public LicenciaViewModel(LicenciaServicio licenciaServicio, DialogoServicio dialogos, Sesion sesion)
    {
        _licenciaServicio = licenciaServicio;
        _dialogos = dialogos;
        _sesion = sesion;
        CodigoInstalacion = licenciaServicio.CodigoInstalacion;
        Actualizar();
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public string CodigoInstalacion { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotonCerrar))]
    private bool esValida;

    [ObservableProperty]
    private string estado = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    public string TextoBotonCerrar => EsValida ? "Cerrar (Esc)" : "Continuar en modo demostración (Esc)";

    // Con una licencia válida instalada, solo quien tiene el permiso la puede reemplazar
    public bool PuedeInstalar => !EsValida || _sesion.Tiene(Permiso.GestionarLicencia);

    private void Actualizar()
    {
        var resultado = _licenciaServicio.Verificar();
        EsValida = resultado.EsValida;
        Estado = resultado.Descripcion;

        if (resultado.DiasParaVencer(DateOnly.FromDateTime(DateTime.Today)) is int dias && dias <= 15)
            Estado += dias <= 0 ? " Vence hoy." : $" Vence en {dias} días: pida la renovación.";

        OnPropertyChanged(nameof(PuedeInstalar));
        InstalarCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CopiarCodigo()
    {
        _dialogos.CopiarAlPortapapeles(CodigoInstalacion);
        Mensaje = string.Empty;
        MensajeExito = "Código copiado. Envíeselo a su proveedor de MiPOS para recibir la licencia.";
    }

    [RelayCommand(CanExecute = nameof(PuedeInstalar))]
    private void Instalar()
    {
        var ruta = _dialogos.ElegirArchivo("Elegir el archivo de licencia", "Licencia de MiPOS (*.lic)|*.lic|Todos los archivos (*.*)|*.*");
        if (ruta is null)
            return;

        try
        {
            var resultado = _licenciaServicio.Instalar(ruta);
            Mensaje = string.Empty;
            MensajeExito = $"Licencia instalada: {resultado.Licencia!.Comercio}.";
            Actualizar();
        }
        catch (Exception ex) when (ex is LicenciaInvalidaException or PermisoDenegadoException)
        {
            MensajeExito = string.Empty;
            Mensaje = ex.Message;
        }
    }

    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();
}
