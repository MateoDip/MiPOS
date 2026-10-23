using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Ventas;
using MiPOS.Seguridad;
using MiPOS.WPF.ViewModels;
using MiPOS.WPF.Vistas;

namespace MiPOS.WPF.Servicios;

/// <summary>
/// Único lugar que abre ventanas de diálogo y mensajes.
/// Así los ViewModels piden "confirmar" o "buscar un producto" sin conocer las ventanas.
/// </summary>
public class DialogoServicio
{
    private readonly IServiceProvider _proveedor;

    public DialogoServicio(IServiceProvider proveedor)
    {
        _proveedor = proveedor;
    }

    // Devuelve true solo si el usuario eligió "Sí". Por defecto queda marcado "No".
    public bool Confirmar(string mensaje, string titulo)
    {
        var resultado = MessageBox.Show(VentanaActiva(), mensaje, titulo,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        return resultado == MessageBoxResult.Yes;
    }

    // Devuelve el texto escrito, o null si el usuario canceló
    public string? PedirCantidad(int cantidadActual)
        => PedirValor("Cambiar cantidad", "Nueva cantidad:", cantidadActual.ToString());

    // Devuelve el texto escrito, o null si el usuario canceló
    public string? PedirDescuento(decimal porcentajeActual)
        => PedirValor("Descuento", "Porcentaje de descuento sobre el total (0 para quitarlo):",
            porcentajeActual > 0 ? porcentajeActual.ToString("0.##") : string.Empty);

    // Devuelve el texto escrito, o null si el usuario canceló
    public string? PedirMontoInicial()
        => PedirValor("Abrir caja", "Efectivo con el que empieza la caja (0 si está vacía):", string.Empty);

    // Devuelve el texto escrito, o null si el usuario canceló
    public string? PedirNombreCategoria()
        => PedirValor("Nueva categoría", "Nombre de la categoría:", string.Empty);

    private string? PedirValor(string titulo, string etiqueta, string valorInicial)
    {
        var viewModel = _proveedor.GetRequiredService<IngresarValorViewModel>();
        viewModel.Titulo = titulo;
        viewModel.Etiqueta = etiqueta;
        viewModel.Texto = valorInicial;

        var ventana = new IngresarValorVentana(viewModel) { Owner = VentanaActiva() };

        return ventana.ShowDialog() == true ? viewModel.Texto : null;
    }

    // Devuelve el producto elegido, o null si el usuario cerró sin elegir
    public Producto? BuscarProducto()
    {
        var viewModel = _proveedor.GetRequiredService<BuscarProductoViewModel>();
        var ventana = new BuscarProductoVentana(viewModel) { Owner = VentanaActiva() };

        return ventana.ShowDialog() == true ? viewModel.ProductoElegido : null;
    }

    // Abre la ventana de cobro. Devuelve la venta registrada, o null si el usuario volvió sin cobrar.
    public Venta? Cobrar(Carrito carrito)
    {
        var viewModel = _proveedor.GetRequiredService<CobroViewModel>();
        viewModel.Iniciar(carrito);

        var ventana = new CobroVentana(viewModel) { Owner = VentanaActiva() };

        return ventana.ShowDialog() == true ? viewModel.VentaRegistrada : null;
    }

    // Pide usuario y contraseña de alguien con el permiso. Devuelve quién autorizó, o null si se canceló.
    public Usuario? PedirAutorizacion(Permiso permiso, string motivo)
    {
        var viewModel = _proveedor.GetRequiredService<AutorizacionViewModel>();
        viewModel.Iniciar(permiso, motivo);

        var ventana = new AutorizacionVentana(viewModel) { Owner = VentanaActiva() };

        return ventana.ShowDialog() == true ? viewModel.Autorizante : null;
    }

    // F7: retiro o ingreso de efectivo. Devuelve true si se registró.
    public bool RegistrarMovimientoCaja()
    {
        var viewModel = _proveedor.GetRequiredService<MovimientoCajaViewModel>();

        return new MovimientoCajaVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true;
    }

    // F11: cierre de caja con arqueo. Devuelve el resumen si se cerró, o null si se volvió sin cerrar.
    public ResumenTurno? CerrarCaja()
    {
        var viewModel = _proveedor.GetRequiredService<CierreCajaViewModel>();
        new CierreCajaVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();

        // Se mira el resultado y no cómo se cerró la ventana: si se cerró la caja, ya está guardado
        return viewModel.Resumen;
    }

    public void GestionarUsuarios()
    {
        var viewModel = _proveedor.GetRequiredService<UsuariosViewModel>();
        viewModel.Iniciar();

        new UsuariosVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // usuario null = alta de un usuario nuevo. Devuelve true si se guardó.
    public bool EditarUsuario(Usuario? usuario)
    {
        var viewModel = _proveedor.GetRequiredService<UsuarioEdicionViewModel>();
        if (usuario is null)
            viewModel.IniciarNuevo();
        else
            viewModel.IniciarEdicion(usuario);

        return new UsuarioEdicionVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true;
    }

    // Devuelve true si se cambió
    public bool CambiarMiContrasena()
    {
        var viewModel = _proveedor.GetRequiredService<CambiarContrasenaViewModel>();
        viewModel.IniciarCambioPropio();

        return new CambiarContrasenaVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true;
    }

    // Devuelve true si se restableció
    public bool RestablecerContrasena(Usuario usuario)
    {
        var viewModel = _proveedor.GetRequiredService<CambiarContrasenaViewModel>();
        viewModel.IniciarRestablecer(usuario);

        return new CambiarContrasenaVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true;
    }

    // Ctrl+P: productos y stock. soloStockBajo abre directamente con el filtro de alertas.
    public void GestionarProductos(bool soloStockBajo)
    {
        var viewModel = _proveedor.GetRequiredService<ProductosViewModel>();
        viewModel.Iniciar(soloStockBajo);

        new ProductosVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // producto null = alta de un producto nuevo. Devuelve el producto guardado, o null si se canceló.
    public Producto? EditarProducto(Producto? producto)
    {
        var viewModel = _proveedor.GetRequiredService<ProductoEdicionViewModel>();
        if (producto is null)
            viewModel.IniciarNuevo();
        else
            viewModel.IniciarEdicion(producto);

        return new ProductoEdicionVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true
            ? viewModel.ProductoGuardado
            : null;
    }

    // Devuelve true si se ajustó
    public bool AjustarStock(Producto producto)
    {
        var viewModel = _proveedor.GetRequiredService<AjusteStockViewModel>();
        viewModel.Iniciar(producto);

        return new AjusteStockVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true;
    }

    public void VerMovimientosStock(Producto producto)
    {
        var viewModel = _proveedor.GetRequiredService<MovimientosStockViewModel>();
        viewModel.Iniciar(producto);

        new MovimientosStockVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // Ctrl+M: compras de mercadería
    public void GestionarCompras()
    {
        var viewModel = _proveedor.GetRequiredService<ComprasViewModel>();
        viewModel.Iniciar();

        new ComprasVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // Devuelve la compra registrada, o null si se canceló
    public Compra? CargarCompra()
    {
        var viewModel = _proveedor.GetRequiredService<CompraViewModel>();
        viewModel.Iniciar();

        return new CompraVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true
            ? viewModel.CompraRegistrada
            : null;
    }

    // Cantidad y costo de un producto de la compra. null si se canceló.
    public (int Cantidad, decimal Costo)? PedirItemCompra(string nombreProducto, int? cantidadActual, decimal? costoSugerido)
    {
        var viewModel = _proveedor.GetRequiredService<CompraItemViewModel>();
        viewModel.Iniciar(nombreProducto, cantidadActual, costoSugerido);

        return new CompraItemVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true
            ? (viewModel.CantidadElegida, viewModel.CostoElegido)
            : null;
    }

    public void GestionarProveedores()
    {
        var viewModel = _proveedor.GetRequiredService<ProveedoresViewModel>();
        viewModel.Iniciar();

        new ProveedoresVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // proveedor null = alta. Devuelve el proveedor guardado, o null si se canceló.
    public Proveedor? EditarProveedor(Proveedor? proveedor)
    {
        var viewModel = _proveedor.GetRequiredService<ProveedorEdicionViewModel>();
        if (proveedor is null)
            viewModel.IniciarNuevo();
        else
            viewModel.IniciarEdicion(proveedor);

        return new ProveedorEdicionVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true
            ? viewModel.ProveedorGuardado
            : null;
    }

    // F2 en el cobro: a quién se factura. Devuelve el cliente elegido, o null si se canceló.
    public DatosReceptor? PedirReceptor(DatosReceptor actual)
    {
        var viewModel = _proveedor.GetRequiredService<ClienteFacturaViewModel>();
        viewModel.Iniciar(actual);

        return new ClienteFacturaVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog() == true
            ? viewModel.ReceptorElegido
            : null;
    }

    // Ctrl+F: facturas sin autorizar
    public void VerFacturasPendientes()
    {
        var viewModel = _proveedor.GetRequiredService<FacturasPendientesViewModel>();
        viewModel.Iniciar();

        new FacturasPendientesVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // Ctrl+H: prueba de impresora, cajón y lector
    public void ProbarHardware()
    {
        var viewModel = _proveedor.GetRequiredService<HardwareViewModel>();
        new HardwareVentana(viewModel) { Owner = VentanaActiva() }.ShowDialog();
    }

    // Ctrl+L (y al arrancar, si no hay licencia válida): ver e instalar la licencia de esta PC
    public void GestionarLicencia()
    {
        var viewModel = _proveedor.GetRequiredService<LicenciaViewModel>();
        var ventana = new LicenciaVentana(viewModel);

        // Al arrancar todavía no hay ninguna ventana abierta
        if (VentanaActiva() is { IsVisible: true } duenio)
        {
            ventana.Owner = duenio;
            ventana.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        ventana.ShowDialog();
    }

    // Ruta del archivo elegido, o null si se canceló. filtro: "Descripción (*.ext)|*.ext"
    public string? ElegirArchivo(string titulo, string filtro)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog { Title = titulo, Filter = filtro, CheckFileExists = true };
        var elegido = VentanaActiva() is { } duenio ? dialogo.ShowDialog(duenio) : dialogo.ShowDialog();
        return elegido == true ? dialogo.FileName : null;
    }

    public void CopiarAlPortapapeles(string texto)
    {
        try
        {
            Clipboard.SetText(texto);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Otro programa tiene ocupado el portapapeles: el código igual se puede seleccionar y copiar a mano
        }
    }

    // true si hay alguna ventana abierta además de la principal (la caja)
    public bool HayDialogoAbierto
        => Application.Current.Windows.OfType<Window>().Count(v => v.IsVisible) > 1;

    // El diálogo nuevo queda encima de la ventana que se está usando
    // (por ejemplo, "Editar usuario" encima de la lista de usuarios, no de la caja)
    private static Window VentanaActiva()
        => Application.Current.Windows.OfType<Window>().FirstOrDefault(v => v.IsActive)
           ?? Application.Current.MainWindow;
}
