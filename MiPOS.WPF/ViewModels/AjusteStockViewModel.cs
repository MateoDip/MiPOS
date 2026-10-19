using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Productos;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

public enum TipoAjusteStock
{
    Sumar,
    Restar,
    StockContado
}

// Una opción de la lista de tipos de ajuste
public record OpcionAjusteStock(TipoAjusteStock Tipo, string Nombre);

/// <summary>
/// Ajuste de stock a mano de un producto: sumar, restar o dejar el stock contado (inventario).
/// </summary>
public partial class AjusteStockViewModel : ObservableObject
{
    private readonly StockServicio _stockServicio;
    private Producto? _producto;

    public AjusteStockViewModel(StockServicio stockServicio)
    {
        _stockServicio = stockServicio;
        tipoSeleccionado = Tipos[0];
    }

    // true = ajustado, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public IReadOnlyList<OpcionAjusteStock> Tipos { get; } =
    [
        new(TipoAjusteStock.Sumar, "Sumar unidades (ej: mercadería encontrada)"),
        new(TipoAjusteStock.Restar, "Restar unidades (ej: rotura, vencimiento, robo)"),
        new(TipoAjusteStock.StockContado, "Stock contado en el local (inventario)")
    ];

    public int LargoMaximoMotivo => StockServicio.LargoMaximoMotivo;

    public string NombreProducto => _producto?.Nombre ?? string.Empty;
    public int StockActual => _producto?.Stock ?? 0;

    public bool Guardando { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EtiquetaCantidad))]
    private OpcionAjusteStock tipoSeleccionado;

    public string EtiquetaCantidad => TipoSeleccionado.Tipo == TipoAjusteStock.StockContado
        ? "Unidades contadas"
        : "Cantidad de unidades";

    [ObservableProperty]
    private string cantidad = string.Empty;

    [ObservableProperty]
    private string motivo = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(Producto producto)
    {
        _producto = producto;
        OnPropertyChanged(nameof(NombreProducto));
        OnPropertyChanged(nameof(StockActual));
    }

    // Flecha arriba / abajo: cambiar el tipo de ajuste
    [RelayCommand]
    private void Subir() => MoverTipo(-1);

    [RelayCommand]
    private void Bajar() => MoverTipo(+1);

    private void MoverTipo(int paso)
    {
        var posicion = Math.Clamp(Tipos.ToList().IndexOf(TipoSeleccionado) + paso, 0, Tipos.Count - 1);
        TipoSeleccionado = Tipos[posicion];

        // "Inventario" es el motivo más común del stock contado
        if (TipoSeleccionado.Tipo == TipoAjusteStock.StockContado && string.IsNullOrWhiteSpace(Motivo))
            Motivo = "Inventario";
    }

    // Enter
    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (_producto is null)
            return;

        var tipo = TipoSeleccionado.Tipo;

        if (!LectorNumeros.IntentarLeerEntero(Cantidad, out var unidades) || (tipo != TipoAjusteStock.StockContado && unidades == 0))
        {
            Mensaje = tipo == TipoAjusteStock.StockContado
                ? "Ingrese cuántas unidades contó (un número entero, puede ser 0)."
                : "Ingrese la cantidad de unidades (un número entero mayor a cero).";
            return;
        }

        var ajustado = false;
        try
        {
            Guardando = true;
            Mensaje = "Guardando...";

            if (tipo == TipoAjusteStock.StockContado)
                await _stockServicio.EstablecerStockAsync(_producto.Id, _producto.Stock, unidades, Motivo);
            else
                await _stockServicio.AjustarAsync(_producto.Id, tipo == TipoAjusteStock.Sumar ? unidades : -unidades, Motivo);

            ajustado = true;
        }
        catch (Exception ex) when (ex is ProductoInvalidoException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. No se guardó el ajuste.";
        }
        finally
        {
            Guardando = false;
        }

        if (ajustado)
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
