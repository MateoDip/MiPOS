using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Ventas;

namespace MiPOS.WPF.ViewModels;

// "partial" es obligatorio: el Toolkit completa la clase con código generado
public partial class CajaViewModel : ObservableObject
{
    private readonly ProductoServicio _productoServicio;
    private readonly Carrito _carrito = new();

    public CajaViewModel(ProductoServicio productoServicio)
    {
        _productoServicio = productoServicio;
    }

    public ReadOnlyObservableCollection<ItemCarrito> Items => _carrito.Items;
    public decimal Total => _carrito.Total;
    public int CantidadArticulos => _carrito.CantidadArticulos;

    // [ObservableProperty] genera una propiedad pública "CodigoIngresado"
    // que avisa a la pantalla cada vez que cambia
    [ObservableProperty]
    private string codigoIngresado = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private ItemCarrito? itemSeleccionado;

    // [RelayCommand] genera "AgregarPorCodigoCommand", que la pantalla ejecuta con Enter
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AgregarPorCodigoAsync()
    {
        // Se toma el código y se limpia el campo enseguida,
        // así el cajero puede escanear el siguiente sin esperar
        var codigo = CodigoIngresado;
        CodigoIngresado = string.Empty;

        if (string.IsNullOrWhiteSpace(codigo))
            return;

        try
        {
            var producto = await _productoServicio.BuscarPorCodigoAsync(codigo);

            if (producto is null)
            {
                Mensaje = $"Producto no encontrado: {codigo.Trim()}";
                return;
            }

            ItemSeleccionado = _carrito.Agregar(producto);
            Mensaje = string.Empty;
            ActualizarTotales();
        }
        catch (Exception)
        {
            Mensaje = "Error al buscar el producto. Verifique la conexión con la base de datos.";
        }
    }

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CantidadArticulos));
    }
}