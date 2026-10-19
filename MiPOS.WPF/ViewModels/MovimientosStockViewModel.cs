using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Historia de movimientos de stock de un producto (solo lectura).
/// </summary>
public partial class MovimientosStockViewModel : ObservableObject
{
    private readonly StockServicio _stockServicio;

    public MovimientosStockViewModel(StockServicio stockServicio)
    {
        _stockServicio = stockServicio;
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public ObservableCollection<MovimientoStock> Movimientos { get; } = new();

    [ObservableProperty]
    private string titulo = "Movimientos de stock";

    [ObservableProperty]
    private string mensaje = "Cargando...";

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(Producto producto)
    {
        Titulo = $"Movimientos de stock · {producto.Nombre} (stock actual: {producto.Stock})";
        _ = CargarAsync(producto.Id);
    }

    private async Task CargarAsync(int productoId)
    {
        try
        {
            foreach (var movimiento in await _stockServicio.ObtenerMovimientosAsync(productoId))
                Movimientos.Add(movimiento);

            Mensaje = Movimientos.Count == 0 ? "El producto todavía no tiene movimientos de stock." : string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar los movimientos. Verifique la conexión con la base de datos.";
        }
    }

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();
}
