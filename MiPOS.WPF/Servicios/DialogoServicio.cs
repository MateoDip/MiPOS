using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Ventas;
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
        var resultado = MessageBox.Show(VentanaPrincipal(), mensaje, titulo,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        return resultado == MessageBoxResult.Yes;
    }

    // Devuelve el texto escrito, o null si el usuario canceló
    public string? PedirCantidad(int cantidadActual)
    {
        var viewModel = _proveedor.GetRequiredService<CantidadViewModel>();
        viewModel.Texto = cantidadActual.ToString();

        var ventana = new CantidadVentana(viewModel) { Owner = VentanaPrincipal() };

        return ventana.ShowDialog() == true ? viewModel.Texto : null;
    }

    // Devuelve el producto elegido, o null si el usuario cerró sin elegir
    public Producto? BuscarProducto()
    {
        var viewModel = _proveedor.GetRequiredService<BuscarProductoViewModel>();
        var ventana = new BuscarProductoVentana(viewModel) { Owner = VentanaPrincipal() };

        return ventana.ShowDialog() == true ? viewModel.ProductoElegido : null;
    }

    // Abre la ventana de cobro. Devuelve la venta registrada, o null si el usuario volvió sin cobrar.
    public Venta? Cobrar(Carrito carrito)
    {
        var viewModel = _proveedor.GetRequiredService<CobroViewModel>();
        viewModel.Iniciar(carrito);

        var ventana = new CobroVentana(viewModel) { Owner = VentanaPrincipal() };

        return ventana.ShowDialog() == true ? viewModel.VentaRegistrada : null;
    }

    private static Window VentanaPrincipal() => Application.Current.MainWindow;
}
