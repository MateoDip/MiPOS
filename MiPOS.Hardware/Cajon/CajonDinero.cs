using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using MiPOS.Hardware.Impresion;

namespace MiPOS.Hardware.Cajon;

/// <summary>
/// Abre el cajón de dinero mandándole un pulso eléctrico, a través de la impresora o de un puerto serie.
/// Es la única excepción a "no usar comandos ESC/POS": el cajón no se puede abrir de otra forma.
/// </summary>
public class CajonDinero
{
    private readonly OpcionesCajon _opciones;
    private readonly OpcionesImpresion _impresion;

    public CajonDinero(OpcionesCajon opciones, OpcionesImpresion impresion)
    {
        _opciones = opciones;
        _impresion = impresion;
    }

    public bool EstaActivo => _opciones.EstaActivo;

    // Lanza HardwareException si no se pudo abrir. Si el cajón está desactivado, no hace nada.
    public Task AbrirAsync()
    {
        if (!_opciones.EstaActivo)
            return Task.CompletedTask;

        var comando = _opciones.LeerComando();

        // Se hace en otro hilo: hablar con la impresora o el puerto puede demorar
        return Task.Run(() =>
        {
            if (_opciones.Modo == ModoCajon.Serial)
                AbrirPorSerial(comando);
            else
                AbrirPorImpresora(comando);
        });
    }

    private void AbrirPorSerial(byte[] comando)
    {
        try
        {
            using var puerto = new SerialPort(_opciones.Puerto, _opciones.Baudios) { WriteTimeout = 2000 };
            puerto.Open();
            puerto.Write(comando, 0, comando.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or ArgumentException or TimeoutException)
        {
            throw new HardwareException($"No se pudo abrir el cajón por el puerto {_opciones.Puerto}: {ex.Message} Revise Cajon:Puerto en appsettings.json.", ex);
        }
    }

    // Manda los bytes tal cual a la cola de la impresora (tipo de datos "RAW": el driver no los modifica)
    private void AbrirPorImpresora(byte[] comando)
    {
        var nombre = !string.IsNullOrWhiteSpace(_opciones.Impresora) ? _opciones.Impresora.Trim()
                   : !string.IsNullOrWhiteSpace(_impresion.Impresora) ? _impresion.Impresora.Trim()
                   : ImpresoraPredeterminada();

        if (!Winspool.OpenPrinter(nombre, out var impresora, IntPtr.Zero))
            throw new HardwareException($"No se encontró la impresora \"{nombre}\" para abrir el cajón. Revise Cajon:Impresora en appsettings.json.",
                new Win32Exception(Marshal.GetLastWin32Error()));

        try
        {
            var documento = new Winspool.DocInfo { NombreDocumento = "Abrir cajón", TipoDatos = "RAW" };
            if (!Winspool.StartDocPrinter(impresora, 1, documento))
                throw new HardwareException($"La impresora \"{nombre}\" no aceptó el pedido de abrir el cajón.", new Win32Exception(Marshal.GetLastWin32Error()));

            try
            {
                Winspool.StartPagePrinter(impresora);
                var memoria = Marshal.AllocHGlobal(comando.Length);
                try
                {
                    Marshal.Copy(comando, 0, memoria, comando.Length);
                    if (!Winspool.WritePrinter(impresora, memoria, comando.Length, out _))
                        throw new HardwareException($"No se pudo enviar el pulso del cajón a la impresora \"{nombre}\".", new Win32Exception(Marshal.GetLastWin32Error()));
                }
                finally
                {
                    Marshal.FreeHGlobal(memoria);
                }
                Winspool.EndPagePrinter(impresora);
            }
            finally
            {
                Winspool.EndDocPrinter(impresora);
            }
        }
        finally
        {
            Winspool.ClosePrinter(impresora);
        }
    }

    private static string ImpresoraPredeterminada()
    {
        var largo = 0;
        Winspool.GetDefaultPrinter(null, ref largo);
        if (largo == 0)
            throw new HardwareException("No hay una impresora predeterminada en Windows para abrir el cajón. Revise Cajon:Impresora en appsettings.json.");

        var nombre = new char[largo];
        if (!Winspool.GetDefaultPrinter(nombre, ref largo))
            throw new HardwareException("No se pudo averiguar la impresora predeterminada de Windows.");

        return new string(nombre, 0, largo - 1); // sin el \0 final
    }

    // Funciones de Windows para mandar datos crudos a una impresora (winspool.drv)
    private static class Winspool
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string NombreDocumento = string.Empty;
            [MarshalAs(UnmanagedType.LPWStr)] public string? ArchivoSalida;
            [MarshalAs(UnmanagedType.LPWStr)] public string TipoDatos = "RAW";
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool OpenPrinter(string nombre, out IntPtr impresora, IntPtr opciones);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr impresora);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool StartDocPrinter(IntPtr impresora, int nivel, [In] DocInfo documento);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndDocPrinter(IntPtr impresora);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool StartPagePrinter(IntPtr impresora);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndPagePrinter(IntPtr impresora);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool WritePrinter(IntPtr impresora, IntPtr datos, int cantidad, out int escritos);

        [DllImport("winspool.drv", EntryPoint = "GetDefaultPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetDefaultPrinter(char[]? nombre, ref int largo);
    }
}
