using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MiPOS.Licencias;

/// <summary>
/// Código que identifica a esta PC (ej: "K7QD-2M4X-PA9C-H3TW"). El comercio se lo pasa a quien
/// vende el sistema y la licencia se emite para ese código.
/// Sale del identificador que Windows le da a cada instalación (MachineGuid): no cambia
/// al reiniciar ni al actualizar el programa, pero sí si se reinstala Windows.
/// </summary>
public static class CodigoInstalacion
{
    // Sin letras que se confunden (I, O, 0, 1)
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly Lazy<string> _deEstaPc = new(() => Calcular(LeerIdentificadorDeWindows()));

    public static string DeEstaPc => _deEstaPc.Value;

    // Separado para poder probarlo con un identificador conocido
    public static string Calcular(string identificadorPc)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"MiPOS|{identificadorPc.Trim().ToUpperInvariant()}"));

        // 16 caracteres de 5 bits (80 bits del hash), en grupos de 4
        var codigo = new StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0)
                codigo.Append('-');

            var bit = i * 5;
            var valor = ((hash[bit / 8] << 8) | hash[bit / 8 + 1]) >> (11 - bit % 8) & 0x1F;
            codigo.Append(Alfabeto[valor]);
        }

        return codigo.ToString();
    }

    // Lo que se escribe a mano se compara sin guiones, espacios ni minúsculas
    public static string Normalizar(string codigo)
        => new string(codigo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string LeerIdentificadorDeWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var clave = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (clave?.GetValue("MachineGuid") is string guid && guid.Length > 0)
                    return guid;
            }
            catch (Exception)
            {
                // Sin permiso para leer el registro: se usa el nombre de la PC
            }
        }

        return Environment.MachineName;
    }
}
