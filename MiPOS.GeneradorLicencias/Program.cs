using System.Globalization;
using System.Text;
using MiPOS.Licencias;

// Emisor de licencias de MiPOS. Uso:
//   dotnet run --project MiPOS.GeneradorLicencias -- claves
//   dotnet run --project MiPOS.GeneradorLicencias -- emitir --codigo K7QD-2M4X-PA9C-H3TW --comercio "Almacén Don Pepe"
//       [--cuit 20123456789] [--numero 2026-0001] [--vence 2027-12-31] [--salida licencia.lic] [--clave ruta]
//   dotnet run --project MiPOS.GeneradorLicencias -- codigo   (muestra el código de instalación de esta PC)

Console.OutputEncoding = Encoding.UTF8;

// La clave privada vive fuera del repositorio, en la carpeta del usuario de Windows
var carpetaClaves = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MiPOS-Licencias");
var rutaClavePorDefecto = Path.Combine(carpetaClaves, "clave-privada.txt");

if (args.Length == 0)
{
    Console.WriteLine("Comandos: claves | emitir --codigo ... --comercio ... | codigo");
    return 1;
}

switch (args[0].ToLowerInvariant())
{
    case "claves":
    {
        if (File.Exists(rutaClavePorDefecto))
        {
            Console.WriteLine($"Ya existe una clave privada en {rutaClavePorDefecto}. No se reemplaza:");
            Console.WriteLine("si se cambia, las licencias emitidas dejan de valer con la próxima versión del programa.");
            return 1;
        }

        var (privada, publica) = ArchivoLicencia.GenerarClaves();
        Directory.CreateDirectory(carpetaClaves);
        File.WriteAllText(rutaClavePorDefecto, privada);

        Console.WriteLine($"Clave privada guardada en: {rutaClavePorDefecto}");
        Console.WriteLine("HAGA UNA COPIA DE SEGURIDAD y no la comparta: con ella cualquiera puede emitir licencias.");
        Console.WriteLine();
        Console.WriteLine("Clave pública (copiarla en MiPOS.Licencias/ArchivoLicencia.cs, ClavePublica):");
        Console.WriteLine(publica);
        return 0;
    }

    case "codigo":
        Console.WriteLine(CodigoInstalacion.DeEstaPc);
        return 0;

    case "emitir":
    {
        var opciones = LeerOpciones(args.Skip(1).ToArray());

        if (!opciones.TryGetValue("codigo", out var codigo) || CodigoInstalacion.Normalizar(codigo).Length != 16)
        {
            Console.WriteLine("Falta --codigo: el código de instalación que muestra MiPOS en la pantalla de licencia (16 letras y números).");
            return 1;
        }

        if (!opciones.TryGetValue("comercio", out var comercio) || string.IsNullOrWhiteSpace(comercio))
        {
            Console.WriteLine("Falta --comercio: el nombre del comercio.");
            return 1;
        }

        DateOnly? vencimiento = null;
        if (opciones.TryGetValue("vence", out var textoVence))
        {
            if (!DateOnly.TryParseExact(textoVence, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            {
                Console.WriteLine("--vence tiene que tener el formato AAAA-MM-DD (ej: 2027-12-31).");
                return 1;
            }
            vencimiento = fecha;
        }

        var rutaClave = opciones.GetValueOrDefault("clave", rutaClavePorDefecto);
        if (!File.Exists(rutaClave))
        {
            Console.WriteLine($"No se encontró la clave privada en {rutaClave}. Use --clave o genere una con el comando \"claves\".");
            return 1;
        }

        var normalizado = CodigoInstalacion.Normalizar(codigo);
        var licencia = new Licencia
        {
            Numero = opciones.GetValueOrDefault("numero", DateTime.Now.ToString("yyyyMMdd-HHmmss")),
            Comercio = comercio.Trim(),
            Cuit = opciones.GetValueOrDefault("cuit", string.Empty).Trim(),
            CodigoInstalacion = string.Join("-", Enumerable.Range(0, 4).Select(i => normalizado.Substring(i * 4, 4))),
            Emision = DateOnly.FromDateTime(DateTime.Today),
            Vencimiento = vencimiento
        };

        var texto = ArchivoLicencia.Firmar(licencia, File.ReadAllText(rutaClave).Trim());
        var salida = opciones.GetValueOrDefault("salida", "licencia.lic");
        File.WriteAllText(salida, texto);

        // Se verifica con la clave pública del programa: si no coincide con la privada, se avisa
        var verificacion = ArchivoLicencia.VerificarTexto(texto, licencia.CodigoInstalacion, licencia.Emision, ArchivoLicencia.ClavePublica);
        Console.WriteLine($"Licencia N° {licencia.Numero} para {licencia.Comercio} ({licencia.CodigoInstalacion}) guardada en {Path.GetFullPath(salida)}");
        if (!verificacion.EsValida)
            Console.WriteLine("ATENCIÓN: la clave privada no corresponde a la clave pública del programa: MiPOS no va a aceptar esta licencia.");
        return verificacion.EsValida ? 0 : 1;
    }

    default:
        Console.WriteLine($"Comando desconocido: {args[0]}");
        return 1;
}

// "--codigo X --comercio Y" → { codigo: X, comercio: Y }
static Dictionary<string, string> LeerOpciones(string[] argumentos)
{
    var opciones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i + 1 < argumentos.Length; i += 2)
        if (argumentos[i].StartsWith("--"))
            opciones[argumentos[i][2..]] = argumentos[i + 1];
    return opciones;
}
