namespace MiPOS.Dominio.Entidades;

// Los números de estos enums son los códigos de ARCA y se guardan en la base: no cambiarlos.

// Condición frente al IVA (del comercio o del cliente)
public enum CondicionIva
{
    ResponsableInscripto = 1,
    Exento = 4,
    ConsumidorFinal = 5,
    Monotributo = 6
}

// Alícuota de IVA de un producto. Exento no es un código de alícuota de ARCA:
// va en el importe exento del comprobante.
public enum AlicuotaIva
{
    Exento = 2,
    Iva0 = 3,
    Iva10_5 = 4,
    Iva21 = 5,
    Iva27 = 6,
    Iva5 = 8,
    Iva2_5 = 9
}

public enum TipoComprobante
{
    FacturaA = 1,
    FacturaB = 6,
    FacturaC = 11
}

public enum EstadoFactura
{
    // Guardada con la venta, esperando el CAE de ARCA (por ejemplo, porque no había Internet)
    Pendiente = 1,

    // ARCA dio el CAE: es un comprobante válido
    Autorizada = 2,

    // ARCA la rechazó por un error en los datos. Un administrador tiene que revisarla.
    Rechazada = 3
}
