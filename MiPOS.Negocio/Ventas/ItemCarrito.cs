using System.ComponentModel;
using System.Runtime.CompilerServices;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Ventas;

/// <summary>
/// Una línea del carrito: un producto con su cantidad.
/// Avisa a la pantalla cuando cambia la cantidad (INotifyPropertyChanged).
/// </summary>
public class ItemCarrito : INotifyPropertyChanged
{
    private int _cantidad;

    public ItemCarrito(Producto producto, int cantidad)
    {
        ProductoId = producto.Id;
        CodigoBarras = producto.CodigoBarras;
        Nombre = producto.Nombre;
        PrecioUnitario = producto.Precio; // se "congela" el precio al momento de agregarlo
        AlicuotaIva = producto.AlicuotaIva;
        _cantidad = cantidad;
    }

    public int ProductoId { get; }
    public string CodigoBarras { get; }
    public string Nombre { get; }
    public decimal PrecioUnitario { get; }
    public AlicuotaIva AlicuotaIva { get; }

    public int Cantidad
    {
        get => _cantidad;
        internal set // solo el Carrito puede cambiarla
        {
            if (_cantidad == value) return;
            _cantidad = value;
            Avisar();
            Avisar(nameof(Subtotal));
        }
    }

    public decimal Subtotal => PrecioUnitario * Cantidad;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Avisar([CallerMemberName] string? propiedad = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}