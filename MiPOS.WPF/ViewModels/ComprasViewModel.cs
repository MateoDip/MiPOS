using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Ctrl+M: últimas compras de mercadería con su detalle. Desde acá se carga una compra nueva
/// y se gestionan los proveedores.
/// </summary>
public partial class ComprasViewModel : ObservableObject
{
    private readonly CompraServicio _compraServicio;
    private readonly DialogoServicio _dialogos;

    public ComprasViewModel(CompraServicio compraServicio, DialogoServicio dialogos)
    {
        _compraServicio = compraServicio;
        _dialogos = dialogos;
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public ObservableCollection<Compra> Compras { get; } = new();

    // El detalle de abajo muestra los productos de la compra seleccionada
    [ObservableProperty]
    private Compra? compraSeleccionada;

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
            var compras = await _compraServicio.ObtenerComprasAsync();

            Compras.Clear();
            foreach (var compra in compras)
                Compras.Add(compra);

            CompraSeleccionada = Compras.FirstOrDefault(c => c.Id == seleccionarId) ?? Compras.FirstOrDefault();
            Mensaje = Compras.Count == 0 ? "Todavía no hay compras registradas." : string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar las compras. Verifique la conexión con la base de datos.";
        }
    }

    [RelayCommand]
    private async Task NuevaCompraAsync()
    {
        MensajeExito = string.Empty;
        if (_dialogos.CargarCompra() is { } compra)
        {
            await CargarAsync(compra.Id);
            MensajeExito = $"Compra N° {compra.Id} registrada por $ {compra.Total:N2}. El stock ya se actualizó.";
        }
    }

    [RelayCommand]
    private void GestionarProveedores() => _dialogos.GestionarProveedores();

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();
}
