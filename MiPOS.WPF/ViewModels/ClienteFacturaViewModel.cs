using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Servicios;

namespace MiPOS.WPF.ViewModels;

// Una opción de la lista de condiciones frente al IVA
public record OpcionCondicionIva(CondicionIva Valor, string Nombre);

/// <summary>
/// F2 en el cobro: a quién se le factura. Documento vacío = consumidor final sin identificar.
/// </summary>
public partial class ClienteFacturaViewModel : ObservableObject
{
    private readonly FacturacionServicio _facturacionServicio;

    public ClienteFacturaViewModel(FacturacionServicio facturacionServicio)
    {
        _facturacionServicio = facturacionServicio;
        condicionSeleccionada = Condiciones[0];
    }

    // true = aceptó, false = canceló. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public IReadOnlyList<OpcionCondicionIva> Condiciones { get; } =
    [
        new(CondicionIva.ConsumidorFinal, "Consumidor final"),
        new(CondicionIva.ResponsableInscripto, "Responsable inscripto"),
        new(CondicionIva.Monotributo, "Monotributista"),
        new(CondicionIva.Exento, "Exento")
    ];

    // El cliente elegido (solo si aceptó)
    public DatosReceptor? ReceptorElegido { get; private set; }

    [ObservableProperty]
    private string documento = string.Empty;

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoComprobante))]
    private OpcionCondicionIva condicionSeleccionada;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Qué factura le va a corresponder (ej: "Factura A")
    public string TextoComprobante
    {
        get
        {
            var tipo = CalculadoraFactura.NombreTipo(
                _facturacionServicio.TipoPara(DatosReceptor.ConsumidorFinal with { CondicionIva = CondicionSeleccionada.Valor }));
            return $"Se emitirá: {tipo[0]}{tipo[1..].ToLowerInvariant()}";
        }
    }

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar(DatosReceptor actual)
    {
        Documento = actual.EstaIdentificado ? actual.DocNumero.ToString() : string.Empty;
        Nombre = actual.Nombre ?? string.Empty;
        CondicionSeleccionada = Condiciones.FirstOrDefault(c => c.Valor == actual.CondicionIva) ?? Condiciones[0];
    }

    // Enter
    [RelayCommand]
    private void Aceptar()
    {
        try
        {
            ReceptorElegido = DatosReceptor.Crear(Documento, Nombre, CondicionSeleccionada.Valor);
            Cerrar?.Invoke(true);
        }
        catch (FacturaInvalidaException ex)
        {
            Mensaje = ex.Message;
        }
    }

    // Vuelve a consumidor final sin identificar
    [RelayCommand]
    private void ConsumidorFinal()
    {
        ReceptorElegido = DatosReceptor.ConsumidorFinal;
        Cerrar?.Invoke(true);
    }

    // Esc
    [RelayCommand]
    private void Cancelar() => Cerrar?.Invoke(false);
}
