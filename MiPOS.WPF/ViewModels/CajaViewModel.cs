using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Ventas;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

// "partial" es obligatorio: el Toolkit completa la clase con código generado
public partial class CajaViewModel : ObservableObject
{
    private const string MensajeCantidadInvalida = "Cantidad inválida: ingrese un número entero mayor a cero.";

    private readonly ProductoServicio _productoServicio;
    private readonly DialogoServicio _dialogos;
    private readonly Carrito _carrito = new();

    public CajaViewModel(ProductoServicio productoServicio, DialogoServicio dialogos)
    {
        _productoServicio = productoServicio;
        _dialogos = dialogos;
    }

    // Avisa a la ventana que terminó una acción, para que devuelva el cursor al campo de código
    public event Action? AccionTerminada;

    public ReadOnlyObservableCollection<ItemCarrito> Items => _carrito.Items;
    public decimal Total => _carrito.Total;
    public int CantidadArticulos => _carrito.CantidadArticulos;

    // [ObservableProperty] genera una propiedad pública "CodigoIngresado"
    // que avisa a la pantalla cada vez que cambia
    [ObservableProperty]
    private string codigoIngresado = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Mensaje en verde (por ejemplo, "Venta registrada"). Se borra al agregar el próximo producto.
    [ObservableProperty]
    private string mensajeExito = string.Empty;

    // Cuando cambia el producto seleccionado, se re-evalúa si se puede eliminar o cambiar cantidad
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarCantidadCommand))]
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
            MensajeExito = string.Empty;
            ActualizarTotales();
        }
        catch (Exception)
        {
            Mensaje = "Error al buscar el producto. Verifique la conexión con la base de datos.";
        }
    }

    // F1: buscar un producto por nombre y agregarlo
    [RelayCommand]
    private void BuscarProducto()
    {
        var producto = _dialogos.BuscarProducto();

        if (producto is not null)
        {
            ItemSeleccionado = _carrito.Agregar(producto);
            Mensaje = string.Empty;
            MensajeExito = string.Empty;
            ActualizarTotales();
        }

        AccionTerminada?.Invoke();
    }

    // F3: cobrar la venta
    [RelayCommand(CanExecute = nameof(HayProductos))]
    private void Cobrar()
    {
        // Si todavía se está buscando un producto escaneado, el total podría cambiar
        // con la ventana de cobro abierta. Se pide esperar.
        if (AgregarPorCodigoCommand.IsRunning)
        {
            Mensaje = "Espere a que termine de cargar el último producto.";
            AccionTerminada?.Invoke();
            return;
        }

        // La ventana de cobro registra la venta. Si se cierra sin cobrar, el carrito queda igual.
        var venta = _dialogos.Cobrar(_carrito);

        if (venta is not null)
        {
            _carrito.Vaciar();
            ItemSeleccionado = null;
            CodigoIngresado = string.Empty;
            Mensaje = string.Empty;
            MensajeExito = venta.Vuelto is decimal vuelto
                ? $"Venta N° {venta.Id} registrada. Vuelto: $ {vuelto:N2}"
                : $"Venta N° {venta.Id} registrada.";
            ActualizarTotales();
        }

        AccionTerminada?.Invoke();
    }

    // F4: eliminar el producto seleccionado
    [RelayCommand(CanExecute = nameof(HayItemSeleccionado))]
    private void Eliminar()
    {
        if (ItemSeleccionado is null)
            return;

        var posicion = Items.IndexOf(ItemSeleccionado);
        _carrito.Eliminar(ItemSeleccionado);

        // Se selecciona el siguiente (o el anterior si era el último),
        // así se puede seguir eliminando con F4
        ItemSeleccionado = Items.Count > 0
            ? Items[Math.Min(posicion, Items.Count - 1)]
            : null;

        Mensaje = string.Empty;
        ActualizarTotales();
        AccionTerminada?.Invoke();
    }

    // F5: cambiar la cantidad del producto seleccionado
    [RelayCommand(CanExecute = nameof(HayItemSeleccionado))]
    private void CambiarCantidad()
    {
        var item = ItemSeleccionado;
        if (item is null)
            return;

        var texto = _dialogos.PedirCantidad(item.Cantidad);

        // null significa que el cajero canceló con Esc: no se cambia nada
        if (texto is not null)
        {
            if (!int.TryParse(texto.Trim(), out var cantidad))
            {
                Mensaje = MensajeCantidadInvalida;
            }
            else
            {
                try
                {
                    // La regla "mayor a cero" la controla el Carrito
                    _carrito.CambiarCantidad(item, cantidad);
                    Mensaje = string.Empty;
                    ActualizarTotales();
                }
                catch (ArgumentOutOfRangeException)
                {
                    Mensaje = MensajeCantidadInvalida;
                }
            }
        }

        AccionTerminada?.Invoke();
    }

    // ESC: cancelar la venta completa (pide confirmación)
    [RelayCommand(CanExecute = nameof(HayProductos))]
    private void CancelarVenta()
    {
        if (_carrito.EstaVacio)
            return;

        if (_dialogos.Confirmar("¿Desea cancelar la venta? Se quitarán todos los productos.", "Cancelar venta"))
        {
            _carrito.Vaciar();
            ItemSeleccionado = null;
            CodigoIngresado = string.Empty;
            Mensaje = string.Empty;
            ActualizarTotales();
        }

        AccionTerminada?.Invoke();
    }

    // Flecha arriba en el campo de código: seleccionar el producto anterior del carrito
    [RelayCommand]
    private void SeleccionarAnterior() => MoverSeleccion(-1);

    // Flecha abajo en el campo de código: seleccionar el producto siguiente del carrito
    [RelayCommand]
    private void SeleccionarSiguiente() => MoverSeleccion(+1);

    private void MoverSeleccion(int paso)
    {
        if (Items.Count == 0)
            return;

        // Si no hay nada seleccionado, la flecha arriba elige el último y la flecha abajo el primero
        var posicion = ItemSeleccionado is null
            ? (paso < 0 ? Items.Count : -1)
            : Items.IndexOf(ItemSeleccionado);

        posicion = Math.Clamp(posicion + paso, 0, Items.Count - 1);
        ItemSeleccionado = Items[posicion];
    }

    private bool HayItemSeleccionado() => ItemSeleccionado is not null;

    private bool HayProductos() => !_carrito.EstaVacio;

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CantidadArticulos));
        CancelarVentaCommand.NotifyCanExecuteChanged();
        CobrarCommand.NotifyCanExecuteChanged();
    }
}
