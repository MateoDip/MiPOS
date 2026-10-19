using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Comun;

namespace MiPOS.Negocio.Facturacion;

/// <summary>
/// A quién se le factura. Por defecto, consumidor final sin identificar.
/// </summary>
public record DatosReceptor(CondicionIva CondicionIva, int DocTipo, long DocNumero, string? Nombre)
{
    // Códigos de tipo de documento de ARCA
    public const int DocCuit = 80;
    public const int DocDni = 96;
    public const int DocSinIdentificar = 99;

    public static readonly DatosReceptor ConsumidorFinal = new(CondicionIva.ConsumidorFinal, DocSinIdentificar, 0, null);

    public bool EstaIdentificado => DocTipo != DocSinIdentificar;

    // Ej: "Consumidor final", "DNI 30123456" o "CUIT 20-12345678-6"
    public string DocumentoTexto => DocTipo switch
    {
        DocCuit => $"CUIT {Cuit.Formatear(DocNumero.ToString())}",
        DocDni => $"DNI {DocNumero}",
        _ => "Consumidor final"
    };

    /// <summary>
    /// Arma el receptor con lo que escribió el cajero. documento: CUIT (11 números, con o sin guiones)
    /// o DNI (7 u 8 números). Vacío = consumidor final sin identificar.
    /// Lanza FacturaInvalidaException si los datos no son válidos.
    /// </summary>
    public static DatosReceptor Crear(string? documento, string? nombre, CondicionIva condicionIva)
    {
        if (!Enum.IsDefined(condicionIva))
            throw new FacturaInvalidaException("Condición frente al IVA inválida.");

        documento = (documento ?? string.Empty).Trim();
        nombre = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();

        if (nombre?.Length > 150)
            throw new FacturaInvalidaException("El nombre del cliente no puede tener más de 150 caracteres.");

        if (documento.Length == 0)
        {
            if (condicionIva != CondicionIva.ConsumidorFinal)
                throw new FacturaInvalidaException("Para un cliente que no es consumidor final hay que ingresar su CUIT.");

            return ConsumidorFinal with { Nombre = nombre };
        }

        var soloNumeros = new string(documento.Where(c => c is not ('-' or '.' or ' ')).ToArray());

        if (soloNumeros.Length == 11)
        {
            if (!Cuit.IntentarNormalizar(soloNumeros, out var cuit))
                throw new FacturaInvalidaException("El CUIT es inválido (revise los números: el último es el verificador).");

            return new DatosReceptor(condicionIva, DocCuit, long.Parse(cuit), nombre);
        }

        if (soloNumeros.Length is 7 or 8 && soloNumeros.All(char.IsAsciiDigit))
        {
            if (condicionIva != CondicionIva.ConsumidorFinal)
                throw new FacturaInvalidaException("Para un cliente que no es consumidor final hay que ingresar su CUIT (no el DNI).");

            return new DatosReceptor(condicionIva, DocDni, long.Parse(soloNumeros), nombre);
        }

        throw new FacturaInvalidaException("Documento inválido: ingrese un CUIT (11 números) o un DNI (7 u 8 números).");
    }
}
