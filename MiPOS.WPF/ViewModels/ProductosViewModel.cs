using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Productos;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Ctrl+P: lista de productos (solo administrador). Alta, edición, ajuste de stock,
/// historia de movimientos y activar/desactivar. Se puede filtrar por los de stock bajo.
/// </summary>
public partial class ProductosViewModel : ObservableObject
{
    private readonly ProductoServicio _productoServicio;
    private readonly StockServicio _stockServicio;
    private readonly DialogoServicio _dialogos;

    // Igual que en el buscador de la caja: si llega la respuesta de una búsqueda vieja, se descarta
    private int _numeroBusqueda;

    public ProductosViewModel(ProductoServicio productoServicio, StockServicio stockServicio, DialogoServicio dialogos)
    {
        _productoServicio = productoServicio;
        _stockServicio = stockServicio;
        _dialogos = dialogos;
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public ObservableCollection<Producto> Productos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoActivar))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(AjustarStockCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerMovimientosCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarActivoCommand))]
    private Producto? productoSeleccionado;

    public string TextoActivar => ProductoSeleccionado?.Activo == false ? "Activar" : "Desactivar";

    // Busca por nombre o por código
    [ObservableProperty]
    private string textoBusqueda = string.Empty;

    [ObservableProperty]
    private bool mostrarDesactivados;

    [ObservableProperty]
    private bool soloStockBajo;

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(bool soloStockBajo)
    {
        // Si cambia el filtro, OnSoloStockBajoChanged ya hace la búsqueda
        if (SoloStockBajo != soloStockBajo)
            SoloStockBajo = soloStockBajo;
        else
            _ = CargarAsync();
    }

    partial void OnTextoBusquedaChanged(string value) => _ = CargarAsync(esperar: true);
    partial void OnMostrarDesactivadosChanged(bool value) => _ = CargarAsync();
    partial void OnSoloStockBajoChanged(bool value) => _ = CargarAsync();

    // esperar: pequeña pausa mientras se escribe, para no consultar la base por cada letra
    private async Task CargarAsync(int? seleccionarId = null, bool esperar = false)
    {
        var numero = ++_numeroBusqueda;
        seleccionarId ??= ProductoSeleccionado?.Id;

        if (esperar)
        {
            await Task.Delay(300);
            if (numero != _numeroBusqueda)
                return;
        }

        try
        {
            List<Producto> productos;

            if (SoloStockBajo)
            {
                // Las alertas son pocas: se filtran por el texto acá mismo
                var texto = TextoBusqueda.Trim();
                productos = (await _stockServicio.ObtenerBajoMinimoAsync())
                    .Where(p => texto.Length == 0
                                || p.Nombre.Contains(texto, StringComparison.CurrentCultureIgnoreCase)
                                || p.CodigoBarras.Contains(texto, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();
            }
            else
            {
                productos = await _productoServicio.ObtenerParaGestionAsync(TextoBusqueda, MostrarDesactivados);
            }

            if (numero != _numeroBusqueda)
                return;

            Productos.Clear();
            foreach (var producto in productos)
                Productos.Add(producto);

            ProductoSeleccionado = Productos.FirstOrDefault(p => p.Id == seleccionarId) ?? Productos.FirstOrDefault();
            Mensaje = Productos.Count == 0
                ? (SoloStockBajo ? "No hay productos con stock bajo." : "No se encontraron productos.")
                : string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            if (numero == _numeroBusqueda)
                Mensaje = "Error al cargar los productos. Verifique la conexión con la base de datos.";
        }
    }

    [RelayCommand]
    private async Task NuevoAsync()
    {
        MensajeExito = string.Empty;
        if (_dialogos.EditarProducto(null) is { } creado)
        {
            await CargarAsync(creado.Id);
            MensajeExito = $"Producto creado: {creado.Nombre}.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayProductoSeleccionado))]
    private async Task EditarAsync()
    {
        if (ProductoSeleccionado is null)
            return;

        MensajeExito = string.Empty;
        if (_dialogos.EditarProducto(ProductoSeleccionado) is not null)
        {
            await CargarAsync(ProductoSeleccionado?.Id);
            MensajeExito = "Cambios guardados.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayProductoSeleccionado))]
    private async Task AjustarStockAsync()
    {
        if (ProductoSeleccionado is null)
            return;

        MensajeExito = string.Empty;
        if (_dialogos.AjustarStock(ProductoSeleccionado))
        {
            await CargarAsync(ProductoSeleccionado?.Id);
            MensajeExito = "Stock ajustado.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayProductoSeleccionado))]
    private void VerMovimientos()
    {
        if (ProductoSeleccionado is not null)
            _dialogos.VerMovimientosStock(ProductoSeleccionado);
    }

    [RelayCommand(CanExecute = nameof(HayProductoSeleccionado))]
    private async Task CambiarActivoAsync()
    {
        if (ProductoSeleccionado is null)
            return;

        var producto = ProductoSeleccionado;
        var activar = !producto.Activo;
        var pregunta = activar
            ? $"¿Activar \"{producto.Nombre}\"? Volverá a aparecer en la caja."
            : $"¿Desactivar \"{producto.Nombre}\"? No se podrá vender hasta que se vuelva a activar.";

        MensajeExito = string.Empty;
        if (!_dialogos.Confirmar(pregunta, activar ? "Activar producto" : "Desactivar producto"))
            return;

        try
        {
            await _productoServicio.CambiarActivoAsync(producto.Id, activar);

            // Si se desactiva y no se muestran los desactivados, se lo deja ver igual para que no "desaparezca"
            if (!activar && !MostrarDesactivados && !SoloStockBajo)
                MostrarDesactivados = true;
            else
                await CargarAsync(producto.Id);

            MensajeExito = activar ? "Producto activado." : "Producto desactivado.";
        }
        catch (Exception ex) when (ex is ProductoInvalidoException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
    }

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();

    private bool HayProductoSeleccionado() => ProductoSeleccionado is not null;
}
