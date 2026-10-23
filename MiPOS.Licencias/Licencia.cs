namespace MiPOS.Licencias;

/// <summary>
/// Lo que dice una licencia. La emite quien vende el sistema (con MiPOS.GeneradorLicencias)
/// para una PC en particular (CodigoInstalacion) y va firmada: si se cambia un solo dato, deja de valer.
/// </summary>
public class Licencia
{
    // Número de licencia, para identificarla en el soporte (ej: "2026-0001")
    public string Numero { get; set; } = string.Empty;

    // A quién se le vendió
    public string Comercio { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;

    // La PC donde vale (ver CodigoInstalacion)
    public string CodigoInstalacion { get; set; } = string.Empty;

    public DateOnly Emision { get; set; }

    // null = no vence (licencia perpetua); con fecha = abono
    public DateOnly? Vencimiento { get; set; }
}

public enum EstadoLicencia
{
    Valida,
    NoEncontrada,
    // El archivo está dañado o fue modificado (la firma no coincide)
    Invalida,
    // Es de otra PC
    OtraInstalacion,
    Vencida
}

// El resultado de revisar la licencia de esta PC
public record ResultadoLicencia(EstadoLicencia Estado, Licencia? Licencia, string CodigoInstalacion)
{
    public bool EsValida => Estado == EstadoLicencia.Valida;

    // Días que faltan para el vencimiento (null si no vence o si no es válida)
    public int? DiasParaVencer(DateOnly hoy)
        => EsValida && Licencia?.Vencimiento is { } vence ? vence.DayNumber - hoy.DayNumber : null;

    public string Descripcion => Estado switch
    {
        EstadoLicencia.Valida => Licencia!.Vencimiento is { } vence
            ? $"Licencia N° {Licencia.Numero} a nombre de {Licencia.Comercio}, válida hasta el {vence:dd/MM/yyyy}."
            : $"Licencia N° {Licencia.Numero} a nombre de {Licencia.Comercio}, sin vencimiento.",
        EstadoLicencia.NoEncontrada => "Esta PC no tiene licencia: el sistema funciona en modo demostración.",
        EstadoLicencia.Invalida => "El archivo de licencia está dañado o fue modificado: el sistema funciona en modo demostración.",
        EstadoLicencia.OtraInstalacion => "La licencia es de otra PC: el sistema funciona en modo demostración.",
        EstadoLicencia.Vencida => $"La licencia venció el {Licencia?.Vencimiento:dd/MM/yyyy}: el sistema funciona en modo demostración.",
        _ => string.Empty
    };
}
