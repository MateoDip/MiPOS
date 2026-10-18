using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Servicios;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// F11 con la caja abierta: cierre con arqueo ciego.
/// Primero el cajero informa cuánto efectivo contó, SIN ver cuánto debería haber.
/// Recién después de cerrar se muestra el resumen con la diferencia.
/// </summary>
public partial class CierreCajaViewModel : ObservableObject
{
    private readonly CajaServicio _cajaServicio;
    private readonly DialogoServicio _dialogos;

    public CierreCajaViewModel(CajaServicio cajaServicio, DialogoServicio dialogos)
    {
        _cajaServicio = cajaServicio;
        _dialogos = dialogos;
    }

    // true = se cerró la caja, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Resultado del cierre (null mientras no se cerró). Si existe, el resumen está a la vista.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cerrada))]
    [NotifyPropertyChangedFor(nameof(Contando))]
    [NotifyPropertyChangedFor(nameof(TextoDiferencia))]
    [NotifyPropertyChangedFor(nameof(HayFaltante))]
    private ResumenTurno? resumen;

    public bool Cerrada => Resumen is not null;
    public bool Contando => Resumen is null;

    public bool HayFaltante => Resumen?.Turno.Diferencia < 0;

    public string TextoDiferencia => Resumen?.Turno.Diferencia switch
    {
        null => string.Empty,
        < 0 and var d => $"Faltan $ {-d:N2}",
        > 0 and var d => $"Sobran $ {d:N2}",
        _ => "Sin diferencia"
    };

    // true mientras se guarda: no se puede cerrar la ventana
    public bool Guardando { get; private set; }

    [ObservableProperty]
    private string efectivoContado = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Enter: en el conteo, cerrar la caja; en el resumen, salir
    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (Cerrada)
        {
            Cerrar?.Invoke(true);
            return;
        }

        if (!LectorNumeros.IntentarLeerDecimal(EfectivoContado, out var contado))
        {
            Mensaje = "Monto inválido: ingrese solo números, con coma para los centavos (ej: 15000 o 15000,50).";
            return;
        }

        if (!_dialogos.Confirmar(
                $"¿Cerrar la caja con $ {contado:N2} de efectivo contado?\nDespués no se puede modificar.",
                "Cerrar caja"))
            return;

        try
        {
            Guardando = true;
            Mensaje = "Cerrando la caja...";
            Resumen = await _cajaServicio.CerrarTurnoAsync(contado);
            Mensaje = string.Empty;
        }
        catch (CajaInvalidaException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al cerrar la caja. No se cerró. Verifique la conexión con la base de datos.";
        }
        finally
        {
            Guardando = false;
        }
    }

    // Esc: en el conteo, volver sin cerrar; en el resumen, salir
    [RelayCommand]
    private void Cancelar()
    {
        if (!Guardando)
            Cerrar?.Invoke(Cerrada);
    }
}
