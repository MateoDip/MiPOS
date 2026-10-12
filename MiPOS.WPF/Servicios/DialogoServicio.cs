using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiPOS.Dominio.Entidades;
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

    // El diálogo nuevo queda encima de la ventana que se está usando
    // (por ejemplo, "Editar usuario" encima de la lista de usuarios, no de la caja)
    private static Window VentanaActiva()
        => Application.Current.Windows.OfType<Window>().FirstOrDefault(v => v.IsActive)
           ?? Application.Current.MainWindow;
}
