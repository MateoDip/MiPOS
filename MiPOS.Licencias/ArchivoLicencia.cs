using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MiPOS.Licencias;

/// <summary>
/// El archivo de licencia (licencia.lic): los datos de la licencia en JSON y su firma digital (ECDSA P-256).
/// Solo quien tiene la clave privada puede firmar; el programa trae la clave pública para verificar.
/// La clave privada NUNCA va en el repositorio ni en el programa (ver MiPOS.Licencias/LEEME.md).
/// </summary>
public static class ArchivoLicencia
{
    // Clave pública de quien emite las licencias. Si se genera otro par de claves
    // (MiPOS.GeneradorLicencias claves), hay que reemplazarla acá y volver a compilar.
    public const string ClavePublica =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEvVTN3Qw8VGB7wgZw8dz8BkdzxWZwHlBKy8K327sM7wORe7AewTaXp9E5MwbUDXRaeTb8hs+yqrW2nuz/9B2ZxA==";

    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    // Lo que se guarda en el archivo
    private sealed class Contenido
    {
        public string Datos { get; set; } = string.Empty; // la licencia en JSON, en Base64
        public string Firma { get; set; } = string.Empty; // firma de Datos, en Base64
    }

    /// <summary>
    /// Lee y verifica el archivo para esta PC (codigoInstalacion) en la fecha "hoy".
    /// </summary>
    public static ResultadoLicencia Verificar(string ruta, string codigoInstalacion, DateOnly hoy)
    {
        if (!File.Exists(ruta))
            return new ResultadoLicencia(EstadoLicencia.NoEncontrada, null, codigoInstalacion);

        try
        {
            return VerificarTexto(File.ReadAllText(ruta), codigoInstalacion, hoy, ClavePublica);
        }
        catch (IOException)
        {
            return new ResultadoLicencia(EstadoLicencia.NoEncontrada, null, codigoInstalacion);
        }
    }

    // Separado para poder probarlo con otra clave pública
    public static ResultadoLicencia VerificarTexto(string texto, string codigoInstalacion, DateOnly hoy, string clavePublica)
    {
        var invalida = new ResultadoLicencia(EstadoLicencia.Invalida, null, codigoInstalacion);

        Licencia? licencia;
        try
        {
            var contenido = JsonSerializer.Deserialize<Contenido>(texto);
            if (contenido is null)
                return invalida;

            var datos = Convert.FromBase64String(contenido.Datos);
            var firma = Convert.FromBase64String(contenido.Firma);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(clavePublica), out _);
            if (!ecdsa.VerifyData(datos, firma, HashAlgorithmName.SHA256))
                return invalida;

            licencia = JsonSerializer.Deserialize<Licencia>(datos);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException)
        {
            return invalida;
        }

        if (licencia is null)
            return invalida;

        if (CodigoInstalacion.Normalizar(licencia.CodigoInstalacion) != CodigoInstalacion.Normalizar(codigoInstalacion))
            return new ResultadoLicencia(EstadoLicencia.OtraInstalacion, licencia, codigoInstalacion);

        if (licencia.Vencimiento is { } vence && hoy > vence)
            return new ResultadoLicencia(EstadoLicencia.Vencida, licencia, codigoInstalacion);

        return new ResultadoLicencia(EstadoLicencia.Valida, licencia, codigoInstalacion);
    }

    // ----- Solo para quien emite las licencias (MiPOS.GeneradorLicencias) -----

    // Arma el texto del archivo de licencia firmado con la clave privada (PKCS#8 en Base64)
    public static string Firmar(Licencia licencia, string clavePrivada)
    {
        var datos = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(licencia));

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(clavePrivada), out _);
        var firma = ecdsa.SignData(datos, HashAlgorithmName.SHA256);

        return JsonSerializer.Serialize(new Contenido
        {
            Datos = Convert.ToBase64String(datos),
            Firma = Convert.ToBase64String(firma)
        }, OpcionesJson);
    }

    // Un par de claves nuevo: (privada PKCS#8, pública SubjectPublicKeyInfo), las dos en Base64
    public static (string Privada, string Publica) GenerarClaves()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()),
                Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()));
    }
}
