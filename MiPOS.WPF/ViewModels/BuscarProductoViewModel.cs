using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Servicios;

namespace MiPOS.WPF.ViewModels;

public partial class BuscarProductoViewModel : ObservableObject
{
    private const int LetrasMinimas = 2;

    private readonly ProductoServicio _productoServicio;

    // Cada búsqueda recibe un número. Si llega la respuesta de una búsqueda vieja
    // (porque el cajero siguió escribiendo), se descarta.
    private int _numeroBusqueda;

    public BuscarProductoViewModel(ProductoServicio productoServicio)
    {
        _productoServicio = productoServicio;
    }

    // true = eligió un producto, false = cerró sin elegir. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public ObservableCollection<Producto> Resultados { get; } = new();

    public Producto? ProductoElegido { get; private set; }

    [ObservableProperty]
    private string textoBusqueda = string.Empty;

    [ObservableProperty]
    private Producto? productoSeleccionado;

    [ObservableProperty]
    private string mensaje = $"Escriba al menos {LetrasMinimas} letras.";

    // El Toolkit llama a este método cada vez que cambia TextoBusqueda
    partial void OnTextoBusquedaChanged(string value) => _ = BuscarAsync(value);

    private async Task BuscarAsync(string texto)
    {
        var numero = ++_numeroBusqueda;

        Resultados.Clear();
        ProductoSeleccionado = null;

        if (texto.Trim().Length < LetrasMinimas)
        {
            Mensaje = $"Escriba al menos {LetrasMinimas} letras.";
            return;
        }

        // Pequeña espera: si el cajero sigue escribiendo, no se consulta la base por cada letra
        await Task.Delay(300);
        if (numero != _numeroBusqueda)
            return;

        try
        {
            var productos = await _productoServicio.BuscarPorNombreAsync(texto);
            if (numero != _numeroBusqueda)
                return;

            foreach (var producto in productos)
                Resultados.Add(producto);

            ProductoSeleccionado = Resultados.FirstOrDefault();
            Mensaje = Resultados.Count == 0 ? "No se encontraron productos." : string.Empty;
        }
        catch (Exception)
        {
            if (numero == _numeroBusqueda)
                Mensaje = "Error al buscar. Verifique la conexión con la base de datos.";
        }
    }

    // Flecha arriba
    [RelayCommand]
    private void Subir() => MoverSeleccion(-1);

    // Flecha abajo
    [RelayCommand]
    private void Bajar() => MoverSeleccion(+1);

    private void MoverSeleccion(int paso)
    {
        if (Resultados.Count == 0)
            return;

        var posicion = ProductoSeleccionado is null ? -1 : Resultados.IndexOf(ProductoSeleccionado);
        posicion = Math.Clamp(posicion + paso, 0, Resultados.Count - 1);
        ProductoSeleccionado = Resultados[posicion];
    }

    // Enter: elegir el producto seleccionado
    [RelayCommand]
    private void Elegir()
    {
        if (ProductoSeleccionado is null)
            return;

        ProductoElegido = ProductoSeleccionado;
        Cerrar?.Invoke(true);
    }

    // Esc: cerrar sin agregar
    [RelayCommand]
    private void Cancelar() => Cerrar?.Invoke(false);
}
