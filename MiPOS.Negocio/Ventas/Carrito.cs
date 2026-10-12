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

    public bool EstaVacio => _items.Count == 0;

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

    public void Eliminar(ItemCarrito item)
    {
        if (!_items.Remove(item))
            throw new InvalidOperationException("El producto no está en el carrito.");
    }

    public void CambiarCantidad(ItemCarrito item, int nuevaCantidad)
    {
        if (nuevaCantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(nuevaCantidad), "La cantidad debe ser mayor a cero.");

        if (!_items.Contains(item))
            throw new InvalidOperationException("El producto no está en el carrito.");

        item.Cantidad = nuevaCantidad;
    }

    public void Vaciar() => _items.Clear();
}