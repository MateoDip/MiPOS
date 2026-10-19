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
/// Alta de un producto nuevo o edición de uno existente.
/// El stock no se edita acá (solo el inicial, al crear): para cambiarlo está "Ajustar stock".
/// </summary>
public partial class ProductoEdicionViewModel : ObservableObject
{
    private readonly ProductoServicio _productoServicio;
    private readonly DialogoServicio _dialogos;

    // null = producto nuevo
    private int? _id;

    // Categoría a elegir cuando terminen de cargarse
    private int? _categoriaId;

    public ProductoEdicionViewModel(ProductoServicio productoServicio, DialogoServicio dialogos)
    {
        _productoServicio = productoServicio;
        _dialogos = dialogos;
    }

    // true = guardado, false = cancelado. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public bool EsNuevo => _id is null;
    public string Titulo => EsNuevo ? "Nuevo producto" : "Editar producto";

    public int LargoMaximoCodigo => ProductoServicio.LargoMaximoCodigo;
    public int LargoMaximoNombre => ProductoServicio.LargoMaximoNombre;

    public ObservableCollection<Categoria> Categorias { get; } = new();

    // El producto guardado (solo si se guardó)
    public Producto? ProductoGuardado { get; private set; }

    public bool Guardando { get; private set; }

    [ObservableProperty]
    private string codigoBarras = string.Empty;

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private Categoria? categoriaSeleccionada;

    [ObservableProperty]
    private string precio = string.Empty;

    [ObservableProperty]
    private string stockMinimo = "0";

    [ObservableProperty]
    private string stockInicial = "0";

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void IniciarNuevo()
    {
        _id = null;
        _ = CargarCategoriasAsync();
    }

    public void IniciarEdicion(Producto producto)
    {
        _id = producto.Id;
        _categoriaId = producto.CategoriaId;
        CodigoBarras = producto.CodigoBarras;
        Nombre = producto.Nombre;
        Precio = producto.Precio.ToString("0.##");
        StockMinimo = producto.StockMinimo.ToString();
        ProductoGuardado = null;
        _ = CargarCategoriasAsync();
    }

    private async Task CargarCategoriasAsync()
    {
        try
        {
            Categorias.Clear();
            foreach (var categoria in await _productoServicio.ObtenerCategoriasAsync())
                Categorias.Add(categoria);

            CategoriaSeleccionada = Categorias.FirstOrDefault(c => c.Id == _categoriaId) ?? Categorias.FirstOrDefault();
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar las categorías. Verifique la conexión con la base de datos.";
        }
    }

    // Para artículos sin código de barras: propone el próximo código interno libre (INT0001...)
    [RelayCommand]
    private async Task GenerarCodigoInternoAsync()
    {
        try
        {
            CodigoBarras = await _productoServicio.GenerarCodigoInternoAsync();
            Mensaje = string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
    }

    [RelayCommand]
    private async Task NuevaCategoriaAsync()
    {
        var nombreCategoria = _dialogos.PedirNombreCategoria();
        if (nombreCategoria is null)
            return;

        try
        {
            var categoria = await _productoServicio.CrearCategoriaAsync(nombreCategoria);
            _categoriaId = categoria.Id;
            await CargarCategoriasAsync();
            Mensaje = string.Empty;
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

    // Enter
    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (!LectorNumeros.IntentarLeerDecimal(Precio, out var precioLeido) || precioLeido == 0)
        {
            Mensaje = "Precio inválido: ingrese un número mayor a cero, con coma para los centavos (ej: 1500 o 1500,50).";
            return;
        }

        if (!LectorNumeros.IntentarLeerEntero(StockMinimo, out var minimo))
        {
            Mensaje = "Stock mínimo inválido: ingrese un número entero (0 = sin alerta).";
            return;
        }

        var inicial = 0;
        if (EsNuevo && !LectorNumeros.IntentarLeerEntero(StockInicial, out inicial))
        {
            Mensaje = "Stock inicial inválido: ingrese un número entero (0 si todavía no hay).";
            return;
        }

        if (CategoriaSeleccionada is null)
        {
            Mensaje = "Elija una categoría.";
            return;
        }

        var datos = new DatosProducto(CodigoBarras, Nombre, CategoriaSeleccionada.Id, precioLeido, minimo);

        try
        {
            Guardando = true;
            Mensaje = "Guardando...";

            if (_id is int id)
            {
                await _productoServicio.ModificarAsync(id, datos);
                ProductoGuardado = await _productoServicio.ObtenerAsync(id);
            }
            else
            {
                ProductoGuardado = await _productoServicio.CrearAsync(datos, inicial);
            }
        }
        catch (Exception ex) when (ex is ProductoInvalidoException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
        finally
        {
            Guardando = false;
        }

        if (ProductoGuardado is not null)
            Cerrar?.Invoke(true);
    }

    // Esc
    [RelayCommand]
    private void Cancelar()
    {
        if (!Guardando)
            Cerrar?.Invoke(false);
    }
}
