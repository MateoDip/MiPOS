using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;

// Prueba automática de las pantallas de MiPOS. Uso:
//   dotnet run --project MiPOS.PruebasPantalla              (todos los recorridos, uno detrás de otro)
//   dotnet run --project MiPOS.PruebasPantalla -- es completo   (un solo recorrido: idioma y modo)
// Modos: completo (venta, caja, administración), abrir (crea los usuarios robot y robotadmin y abre la caja),
// cajero (descuento autorizado, factura A simulada, F10), admin (compras, stock, usuarios),
// sinconexion (venta con la base central caída), cerrar (sube las ventas sin conexión y cierra la caja).

Console.OutputEncoding = Encoding.UTF8;

// Crea usuarios con contraseña conocida y deja datos de prueba: nunca contra la base de un comercio
var conexion = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json")
    .Build().GetConnectionString("MiPOS") ?? string.Empty;
if (!conexion.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
    && Environment.GetEnvironmentVariable("MIPOS_PERMITIR_PRUEBAS") != "1")
{
    Console.WriteLine("La cadena de conexión no es la LocalDB de desarrollo: no se ejecuta la prueba.");
    Console.WriteLine("Si de verdad es una base de pruebas, defina la variable MIPOS_PERMITIR_PRUEBAS=1.");
    return 1;
}

if (args.Length >= 2)
    return Robot.Ejecutar(args[0], args[1]);

// Todos los recorridos, cada uno en su propio proceso. El orden importa:
// "abrir" crea los usuarios de prueba y deja la caja abierta; "cerrar" sube la venta hecha sin conexión.
string[] recorridos = ["es completo", "zh completo", "es abrir", "zh cajero", "es admin", "es sinconexion", "es cerrar"];
var errores = 0;
foreach (var recorrido in recorridos)
{
    Console.WriteLine($"\n===== {recorrido} =====");
    using var proceso = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, recorrido)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        StandardOutputEncoding = Encoding.UTF8
    })!;
    Console.Write(await proceso.StandardOutput.ReadToEndAsync());

    if (!proceso.WaitForExit(TimeSpan.FromMinutes(3)))
    {
        proceso.Kill();
        Console.WriteLine("ERROR: el recorrido no terminó en 3 minutos (¿quedó un diálogo abierto?)");
        errores++;
        continue;
    }

    errores += proceso.ExitCode;
}

Console.WriteLine($"\n===== RESULTADO: {(errores == 0 ? "todo OK" : $"{errores} errores")} =====");
return errores == 0 ? 0 : 1;
