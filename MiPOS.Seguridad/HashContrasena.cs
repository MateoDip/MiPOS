using System.Security.Cryptography;

namespace MiPOS.Seguridad;

/// <summary>
/// Convierte una contraseña en un "hash": una huella de la que no se puede recuperar la contraseña.
/// En la base se guarda solo el hash, nunca la contraseña.
///
/// Formato guardado: PBKDF2$SHA256$600000$sal$hash
/// (se guarda el algoritmo y las iteraciones para poder cambiarlos en el futuro
/// sin romper las contraseñas que ya existen)
/// </summary>
public static class HashContrasena
{
    private const string Algoritmo = "PBKDF2";
    private const string FuncionHash = "SHA256";

    // Cantidad de repeticiones recomendada por OWASP para PBKDF2-SHA256.
    // Hace que probar millones de contraseñas sea muy lento para un atacante.
    private const int Iteraciones = 600_000;

    private const int BytesSal = 16;
    private const int BytesHash = 32;

    // En una PC lenta puede tardar cerca de medio segundo: llamarlo fuera del hilo de la pantalla (Task.Run)
    public static string Generar(string contrasena)
    {
        ArgumentException.ThrowIfNullOrEmpty(contrasena);

        // La sal es aleatoria y distinta para cada contraseña:
        // dos usuarios con la misma contraseña quedan con hashes distintos
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);

        return $"{Algoritmo}${FuncionHash}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    // En una PC lenta puede tardar cerca de medio segundo: llamarlo fuera del hilo de la pantalla (Task.Run)
    public static bool Verificar(string contrasena, string hashGuardado)
    {
        if (string.IsNullOrEmpty(contrasena) || string.IsNullOrEmpty(hashGuardado))
            return false;

        var partes = hashGuardado.Split('$');
        if (partes.Length != 5 || partes[0] != Algoritmo || partes[1] != FuncionHash)
            return false;

        if (!int.TryParse(partes[2], out var iteraciones) || iteraciones <= 0)
            return false;

        byte[] sal;
        byte[] hashEsperado;
        try
        {
            sal = Convert.FromBase64String(partes[3]);
            hashEsperado = Convert.FromBase64String(partes[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        // Un hash guardado incompleto o vacío nunca debe dar una contraseña por válida
        if (sal.Length != BytesSal || hashEsperado.Length != BytesHash)
            return false;

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, BytesHash);

        // Compara siempre todos los bytes, tarde lo mismo si difiere el primero o el último:
        // así no se puede deducir el hash midiendo cuánto tarda la comparación
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
