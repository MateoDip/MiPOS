using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MiPOS.Facturacion;

/// <summary>
/// Texto del código QR que ARCA exige imprimir en los comprobantes electrónicos (RG 4892).
/// Es una dirección web con los datos del comprobante en formato JSON codificado en Base64.
/// </summary>
public static class CodigoQrArca
{
    private const string Direccion = "https://www.afip.gob.ar/fe/qr/?p=";

    public static string ArmarUrl(DateOnly fecha, long cuitEmisor, int puntoVenta, int tipoComprobante, long numero,
                                  decimal importeTotal, int docTipo, long docNumero, string cae)
    {
        // El orden y los nombres de los campos son los de la especificación de ARCA
        var datos = new Dictionary<string, object>
        {
            ["ver"] = 1,
            ["fecha"] = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["cuit"] = cuitEmisor,
            ["ptoVta"] = puntoVenta,
            ["tipoCmp"] = tipoComprobante,
            ["nroCmp"] = numero,
            ["importe"] = importeTotal,
            ["moneda"] = "PES",
            ["ctz"] = 1,
            ["tipoDocRec"] = docTipo,
            ["nroDocRec"] = docNumero,
            ["tipoCodAut"] = "E", // E = CAE
            ["codAut"] = long.Parse(cae, CultureInfo.InvariantCulture)
        };

        var json = JsonSerializer.Serialize(datos);
        return Direccion + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
