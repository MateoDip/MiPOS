using System.Collections.ObjectModel;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Ventas;

public class Carrito
{
    private readonly ObservableCollection<ItemCarrito> _items = new();

    public Carrito()
    {
        Items = new ReadOnlyObservableCollection<ItemCarrito>(_items);
    }

    // De "solo lectura" hacia afuera: para modificar hay que usar los métodos del Carrito
    public ReadOnlyObservableCollection<ItemCarrito> Items { get; }

    public decimal Total => _items.Sum(i => i.Subtotal);

    public int CantidadArticulos => _items.Sum(i => i.Cantidad);

    public ItemCarrito Agregar(Producto producto, int cantidad = 1)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor a cero.");

        // Si el producto ya está en el carrito, se suma la cantidad
        var existente = _items.FirstOrDefault(i => i.ProductoId == producto.Id);
        if (existente is not null)
        {
            existente.Cantidad += cantidad;
            return existente;
        }

        var nuevo = new ItemCarrito(producto, cantidad);
        _items.Add(nuevo);
        return nuevo;
    }

    public void Vaciar() => _items.Clear();
}