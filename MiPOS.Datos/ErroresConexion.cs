using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace MiPOS.Datos;

/// <summary>
/// Distingue un corte de conexión con SQL Server (la red, el servidor apagado) de un error común
/// (un dato repetido, una regla de la base). Solo en el primer caso tiene sentido seguir sin conexión.
/// </summary>
public static class ErroresConexion
{
    // Números de error de SQL Server que significan "no se pudo hablar con el servidor"
    private static readonly HashSet<int> NumerosDeConexion = new()
    {
        -2,     // tiempo de espera agotado
        -1, 2, 53, 64, 121, 233, 258, 1231, // no se encontró el servidor o se cortó la red
        4060,   // no se puede abrir la base
        10053, 10054, 10060, 10061, 11001, // errores de red de Windows
        40197, 40501, 40613 // servidor ocupado o no disponible
    };

    public static bool EsErrorDeConexion(Exception? error)
    {
        for (var actual = error; actual is not null; actual = actual.InnerException)
        {
            switch (actual)
            {
                case SqlException sql:
                    // Clase 20 o más: error grave de la conexión
                    return sql.Class >= 20 || sql.Errors.Cast<SqlError>().Any(e => NumerosDeConexion.Contains(e.Number));
                case SocketException:
                case TimeoutException:
                    return true;
            }
        }

        return false;
    }
}
