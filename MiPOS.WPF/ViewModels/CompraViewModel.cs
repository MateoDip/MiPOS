using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Compras;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Carga de una compra nueva: proveedor, número de comprobante y productos
/// (escaneados o buscados por nombre) con su cantidad y costo. Al registrarla se suma al stock.
/// </summary>
public partial class CompraViewModel : ObservableObject
{
    private readonly CompraServicio _compraServicio;
    private readonly ProductoServicio _productoServicio;
    private readonly DialogoServicio _dialogos;

    public CompraViewModel(CompraServicio compraServicio, ProductoServicio productoServicio, DialogoServicio dialogos)
    {
        _compraServicio = compraServicio;
        _productoServicio = productoServicio;
        _dialogos = dialogos;
    }

    // true = registrada, false = cancelada. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    // Avisa a la ventana que terminó una acción, para que devuelva el cursor al campo de código
    public event Action? AccionTerminada;

    public int LargoMaximoComprobante => CompraServicio.LargoMaximoComprobante;

    public ObservableCollection<Proveedor> Proveedores { get; } = new();

    public ObservableCollection<ItemCompra> Items { get; } = new();

    public decimal Total => Items.Sum(i => i.Subtotal);

    public int CantidadUnidades => Items.Sum(i => i.Cantidad);

    // La compra guardada (solo si se registró)
    public Compra? CompraRegistrada { get; private set; }

    public bool Guardando { get; private set; }

    [ObservableProperty]
    private Proveedor? proveedorSeleccionado;

    [ObservableProperty]
    private string numeroComprobante = string.Empty;

    [ObservableProperty]
    private string codigoIngresado = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuitarCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarCommand))]
    private ItemCompra? itemSeleccionado;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar() => _ = CargarProveedoresAsync();

    private async Task CargarProveedoresAsync()
    {
        try
        {
            foreach (var proveedor in await _compraServicio.ObtenerProveedoresAsync(incluirInactivos: false))
                Proveedores.Add(proveedor);

            Mensaje = Proveedores.Count == 0
                ? "No hay proveedores activos. Cierre esta ventana y cárguelos con el botón \"Proveedores\"."
                : string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar los proveedores. Verifique la conexión con la base de datos.";
        }
    }

    // Enter en el campo de código
    [RelayCommand]
    private async Task AgregarPorCodigoAsync()
    {
        var codigo = CodigoIngresado;
        CodigoIngresado = string.Empty;

        if (string.IsNullOrWhiteSpace(codigo))
            return;

        try
        {
            var producto = await _productoServicio.BuscarPorCodigoAsync(codigo);

            if (producto is null)
                Mensaje = $"Producto no encontrado: {codigo.Trim()}. Si es nuevo, dele de alta en Productos (Ctrl+P).";
            else
                await AgregarAsync(producto);
        }
        catch (Exception)
        {
            Mensaje = "Error al buscar el producto. Verifique la conexión con la base de datos.";
        }

        AccionTerminada?.Invoke();
    }

    // F1: buscar por nombre
    [RelayCommand]
    private async Task BuscarProductoAsync()
    {
        if (_dialogos.BuscarProducto() is { } producto)
            await AgregarAsync(producto);

        AccionTerminada?.Invoke();
    }

    // Pide cantidad y costo. Si el producto ya está en la compra, se edita esa línea.
    private async Task AgregarAsync(Producto producto)
    {
        var existente = Items.FirstOrDefault(i => i.ProductoId == producto.Id);

        decimal? costoSugerido = existente?.CostoUnitario;
        if (costoSugerido is null)
        {
            try { costoSugerido = await _compraServicio.ObtenerUltimoCostoAsync(producto.Id); }
            catch (Exception) { } // es solo una sugerencia
        }

        var datos = _dialogos.PedirItemCompra(producto.Nombre, existente?.Cantidad, costoSugerido);
        if (datos is not { } elegido)
            return;

        var item = new ItemCompra(producto.Id, producto.CodigoBarras, producto.Nombre, elegido.Cantidad, elegido.Costo);
        Reemplazar(existente, item);
        Mensaje = existente is null ? string.Empty : $"{producto.Nombre} ya estaba en la compra: se actualizó la línea.";
    }

    // F5: cambiar cantidad y costo de la línea seleccionada
    [RelayCommand(CanExecute = nameof(HayItemSeleccionado))]
    private void Cambiar()
    {
        if (ItemSeleccionado is not { } item)
            return;

        if (_dialogos.PedirItemCompra(item.NombreProducto, item.Cantidad, item.CostoUnitario) is { } elegido)
            Reemplazar(item, item with { Cantidad = elegido.Cantidad, CostoUnitario = elegido.Costo });

        AccionTerminada?.Invoke();
    }

    // F4: quitar la línea seleccionada
    [RelayCommand(CanExecute = nameof(HayItemSeleccionado))]
    private void Quitar()
    {
        if (ItemSeleccionado is not { } item)
            return;

        var posicion = Items.IndexOf(item);
        Items.Remove(item);
        ItemSeleccionado = Items.Count > 0 ? Items[Math.Min(posicion, Items.Count - 1)] : null;
        ActualizarTotales();
        AccionTerminada?.Invoke();
    }

    private void Reemplazar(ItemCompra? anterior, ItemCompra nuevo)
    {
        var posicion = anterior is null ? -1 : Items.IndexOf(anterior);
        if (posicion >= 0)
            Items[posicion] = nuevo;
        else
            Items.Add(nuevo);

        ItemSeleccionado = nuevo;
        ActualizarTotales();
    }

    // F3: registrar la compra
    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (ProveedorSeleccionado is null)
        {
            Mensaje = "Elija el proveedor.";
            return;
        }

        if (Items.Count == 0)
        {
            Mensaje = "Agregue al menos un producto.";
            return;
        }

        if (!_dialogos.Confirmar($"¿Registrar la compra a {ProveedorSeleccionado.Nombre} por $ {Total:N2}? Se sumarán {CantidadUnidades} unidades al stock.",
                "Registrar compra"))
        {
            AccionTerminada?.Invoke();
            return;
        }

        try
        {
            Guardando = true;
            Mensaje = "Registrando compra...";
            CompraRegistrada = await _compraServicio.RegistrarAsync(ProveedorSeleccionado.Id, NumeroComprobante, Items.ToList());
        }
        catch (Exception ex) when (ex is CompraInvalidaException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al registrar la compra. No se guardó. Verifique la conexión con la base de datos.";
        }
        finally
        {
            Guardando = false;
        }

        if (CompraRegistrada is not null)
            Cerrar?.Invoke(true);
    }

    // Esc: salir sin registrar (pide confirmación si ya se cargaron productos)
    [RelayCommand]
    private void Cancelar()
    {
        if (Guardando)
            return;

        if (Items.Count > 0 && !_dialogos.Confirmar("¿Salir sin registrar la compra? Se perderán los productos cargados.", "Cancelar compra"))
        {
            AccionTerminada?.Invoke();
            return;
        }

        Cerrar?.Invoke(false);
    }

    private bool HayItemSeleccionado() => ItemSeleccionado is not null;

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CantidadUnidades));
    }
}
