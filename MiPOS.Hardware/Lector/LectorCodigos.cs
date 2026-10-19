using System.IO;
using System.IO.Ports;
using System.Text;

namespace MiPOS.Hardware.Lector;

public enum ModoLector
{
    // El lector se comporta como un teclado (USB HID): escribe el código y un Enter en el campo
    // que tenga el cursor. No necesita nada del programa. Es el caso más común.
    Teclado,

    // El lector manda los códigos por un puerto serie (o USB que aparece como puerto COM)
    Serial
}

/// <summary>
/// Configuración del lector de códigos de barras. Sección "Lector" de appsettings.json.
/// </summary>
public class OpcionesLector
{
    public ModoLector Modo { get; set; } = ModoLector.Teclado;

    // Solo Modo = Serial
    public string Puerto { get; set; } = "COM3";
    public int Baudios { get; set; } = 9600;
}

/// <summary>
/// Lee códigos de un lector conectado por puerto serie. Cada código termina con un fin de línea
/// (Enter), como mandan casi todos los lectores. En Modo = Teclado no hace nada.
/// </summary>
public sealed class LectorCodigos : IDisposable
{
    private readonly OpcionesLector _opciones;
    private readonly StringBuilder _pendiente = new();
    private SerialPort? _puerto;
    private Action<string>? _alLeer;

    public LectorCodigos(OpcionesLector opciones)
    {
        _opciones = opciones;
    }

    public bool UsaPuertoSerie => _opciones.Modo == ModoLector.Serial;

    /// <summary>
    /// Empieza a escuchar el puerto. alLeer se llama con cada código leído, desde otro hilo
    /// (quien lo usa tiene que pasarlo al hilo de la pantalla). Lanza HardwareException si no se pudo abrir.
    /// </summary>
    public void Iniciar(Action<string> alLeer)
    {
        if (!UsaPuertoSerie || _puerto is not null)
            return;

        _alLeer = alLeer;
        try
        {
            _puerto = new SerialPort(_opciones.Puerto, _opciones.Baudios) { Encoding = Encoding.ASCII };
            _puerto.DataReceived += AlRecibirDatos;
            _puerto.Open();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Detener();
            throw new HardwareException($"No se pudo abrir el lector de códigos en el puerto {_opciones.Puerto}: {ex.Message} Revise Lector:Puerto en appsettings.json.", ex);
        }
    }

    public void Detener()
    {
        if (_puerto is null)
            return;

        _puerto.DataReceived -= AlRecibirDatos;
        try { _puerto.Close(); } catch (IOException) { }
        _puerto.Dispose();
        _puerto = null;
        _alLeer = null;
    }

    public void Dispose() => Detener();

    private void AlRecibirDatos(object sender, SerialDataReceivedEventArgs e)
    {
        string texto;
        try
        {
            texto = _puerto?.ReadExisting() ?? string.Empty;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return; // el puerto se cerró mientras llegaban datos
        }

        foreach (var codigo in Separar(texto))
            _alLeer?.Invoke(codigo);
    }

    /// <summary>
    /// Junta lo que va llegando (puede venir en pedazos) y devuelve los códigos completos.
    /// Público para poder probarlo sin un lector.
    /// </summary>
    public IReadOnlyList<string> Separar(string texto)
    {
        var codigos = new List<string>();
        lock (_pendiente)
        {
            foreach (var letra in texto)
            {
                if (letra is '\r' or '\n')
                {
                    var codigo = _pendiente.ToString().Trim();
                    _pendiente.Clear();
                    if (codigo.Length > 0)
                        codigos.Add(codigo);
                }
                else
                {
                    _pendiente.Append(letra);
                }
            }
        }
        return codigos;
    }
}
