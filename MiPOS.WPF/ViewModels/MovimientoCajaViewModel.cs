using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

// Una opción de la lista "Retiro / Ingreso"
public record OpcionMovimiento(TipoMovimientoCaja Tipo, string Nombre);

/// <summary>
/// F7: registrar un retiro o un ingreso de efectivo en la caja.
/// El retiro pide autorización si el usuario conectado no tiene el permiso.
/// </summary>
public partial class MovimientoCajaViewModel : ObservableObject
{
    private readonly CajaServicio _cajaServicio;
    private readonly DialogoServicio _dialogos;
    private readonly Sesion _sesion;

    public MovimientoCajaViewModel(CajaServicio cajaServicio, DialogoServicio dialogos, Sesion sesion)
    {
        _cajaServicio = cajaServicio;
        _dialogos = dialogos;
        _sesion = sesion;
        tipoSeleccionado = Tipos[0];
    }

    // true = registrado, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // El retiro va primero: es el movimiento más común
    public IReadOnlyList<OpcionMovimiento> Tipos { get; } =
    [
        new(TipoMovimientoCaja.Retiro, "Retiro de efectivo"),
        new(TipoMovimientoCaja.Ingreso, "Ingreso de efectivo")
    ];

    public int LargoMaximoMotivo => CajaServicio.LargoMaximoMotivo;

    // El movimiento guardado (solo si se registró)
    public MovimientoCaja? MovimientoRegistrado { get; private set; }

    // true mientras se guarda: no se puede cerrar la ventana
    public bool Guardando { get; private set; }

    [ObservableProperty]
    private OpcionMovimiento tipoSeleccionado;

    [ObservableProperty]
    private string monto = string.Empty;

    [ObservableProperty]
    private string motivo = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Flecha arriba / abajo: cambiar entre retiro e ingreso
    [RelayCommand]
    private void CambiarTipo() => TipoSeleccionado = TipoSeleccionado == Tipos[0] ? Tipos[1] : Tipos[0];

    // Enter
    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (!LectorNumeros.IntentarLeerDecimal(Monto, out var importe) || importe == 0)
        {
            Mensaje = "Monto inválido: ingrese un número mayor a cero, con coma para los centavos (ej: 5000 o 5000,50).";
            return;
        }

        if (string.IsNullOrWhiteSpace(Motivo))
        {
            Mensaje = "Ingrese el motivo.";
            return;
        }

        var tipo = TipoSeleccionado.Tipo;
        int? autorizadoPorId = null;

        if (tipo == TipoMovimientoCaja.Retiro)
        {
            // Si el usuario conectado tiene el permiso, autoriza él mismo
            var autorizante = _sesion.Tiene(Permiso.RetirarEfectivo)
                ? _sesion.UsuarioActual
                : _dialogos.PedirAutorizacion(Permiso.RetirarEfectivo, $"Retiro de efectivo de $ {importe:N2}");

            if (autorizante is null)
            {
                Mensaje = "El retiro no fue autorizado.";
                return;
            }

            autorizadoPorId = autorizante.Id;
        }

        try
        {
            Guardando = true;
            Mensaje = "Registrando...";
            MovimientoRegistrado = await _cajaServicio.RegistrarMovimientoAsync(tipo, importe, Motivo, autorizadoPorId);
        }
        catch (Exception ex) when (ex is CajaInvalidaException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al registrar el movimiento. No se guardó. Verifique la conexión con la base de datos.";
        }
        finally
        {
            Guardando = false;
        }

        if (MovimientoRegistrado is not null)
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
