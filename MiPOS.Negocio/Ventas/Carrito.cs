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

    // Suma de los productos, antes del descuento
    public decimal Subtotal => _items.Sum(i => i.Subtotal);

    // Descuento sobre el total de la venta, en porcentaje (0 = sin descuento)
    public decimal DescuentoPorcentaje { get; private set; }

    // Quién autorizó el descuento (un usuario con permiso para aplicarlo)
    public int? DescuentoAutorizadoPorId { get; private set; }

    public bool TieneDescuento => DescuentoPorcentaje > 0;

    // Monto descontado, redondeado a centavos. Se recalcula si cambian los productos.
    public decimal Descuento =>
        Math.Round(Subtotal * DescuentoPorcentaje / 100m, 2, MidpointRounding.AwayFromZero);

    // Lo que paga el cliente
    public decimal Total => Subtotal - Descuento;

    public int CantidadArticulos => _items.Sum(i => i.Cantidad);

    public bool EstaVacio => _items.Count == 0;

    // Mayor a 0, hasta 100, con hasta 2 decimales
    public static bool EsPorcentajeDescuentoValido(decimal porcentaje)
        => porcentaje > 0 && porcentaje <= 100 && decimal.Round(porcentaje, 2) == porcentaje;

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

        // Si no quedan productos, el descuento ya no tiene sentido
        if (EstaVacio)
            QuitarDescuento();
    }

    public void CambiarCantidad(ItemCarrito item, int nuevaCantidad)
    {
        if (nuevaCantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(nuevaCantidad), "La cantidad debe ser mayor a cero.");

        if (!_items.Contains(item))
            throw new InvalidOperationException("El producto no está en el carrito.");

        item.Cantidad = nuevaCantidad;
    }

    // El permiso del que autoriza lo controla VentaServicio al registrar la venta
    public void AplicarDescuento(decimal porcentaje, int autorizadoPorId)
    {
        if (EstaVacio)
            throw new InvalidOperationException("No se puede aplicar un descuento sin productos.");

        if (!EsPorcentajeDescuentoValido(porcentaje))
            throw new ArgumentOutOfRangeException(nameof(porcentaje), "El descuento debe ser mayor a 0 y hasta 100%, con hasta 2 decimales.");

        DescuentoPorcentaje = porcentaje;
        DescuentoAutorizadoPorId = autorizadoPorId;
    }

    public void QuitarDescuento()
    {
        DescuentoPorcentaje = 0;
        DescuentoAutorizadoPorId = null;
    }

    public void Vaciar()
    {
        _items.Clear();
        QuitarDescuento();
    }
}