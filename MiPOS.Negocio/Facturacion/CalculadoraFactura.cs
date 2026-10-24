using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.Facturacion;

// Importes de una factura. Total = Neto + Exento + Iva.
public record ImportesFactura(decimal Neto, decimal Exento, decimal Iva, decimal Total, IReadOnlyList<FacturaIva> Alicuotas);

/// <summary>
/// Reglas fiscales que no necesitan base de datos: qué letra de factura corresponde
/// y cómo se separa el IVA del precio final.
/// </summary>
public static class CalculadoraFactura
{
    public static decimal Tasa(AlicuotaIva alicuota) => alicuota switch
    {
        AlicuotaIva.Iva21 => 0.21m,
        AlicuotaIva.Iva10_5 => 0.105m,
        AlicuotaIva.Iva27 => 0.27m,
        AlicuotaIva.Iva5 => 0.05m,
        AlicuotaIva.Iva2_5 => 0.025m,
        _ => 0m // Iva0 y Exento
    };

    // Ej: "21%", "10,5%", "Exento"
    public static string Nombre(AlicuotaIva alicuota)
        => alicuota == AlicuotaIva.Exento ? "Exento" : $"{Tasa(alicuota) * 100:0.##}%";

    // Monotributo y Exento emiten C. Responsable Inscripto: A a otro inscripto o monotributista, B al resto.
    public static TipoComprobante DeterminarTipo(CondicionIva emisor, CondicionIva receptor)
    {
        if (emisor != CondicionIva.ResponsableInscripto)
            return TipoComprobante.FacturaC;

        return receptor is CondicionIva.ResponsableInscripto or CondicionIva.Monotributo
            ? TipoComprobante.FacturaA
            : TipoComprobante.FacturaB;
    }

    // Ej: "FACTURA B"
    public static string NombreTipo(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "FACTURA A",
        TipoComprobante.FacturaB => "FACTURA B",
        TipoComprobante.FacturaC => "FACTURA C",
        _ => "COMPROBANTE"
    };

    // Para las pantallas: "Factura A" (la letra queda en mayúscula)
    public static string NombreCorto(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "Factura A",
        TipoComprobante.FacturaB => "Factura B",
        TipoComprobante.FacturaC => "Factura C",
        _ => "Comprobante"
    };

    /// <summary>
    /// Separa neto e IVA. Las líneas tienen precio final (con IVA). El descuento de la venta
    /// se reparte entre las alícuotas en proporción a lo que suma cada una; la última se lleva
    /// el redondeo, así todo suma exactamente el total.
    /// </summary>
    public static ImportesFactura Calcular(TipoComprobante tipo, IEnumerable<(AlicuotaIva Alicuota, decimal Subtotal)> lineas,
                                           decimal descuento)
    {
        var grupos = lineas
            .GroupBy(l => l.Alicuota)
            .Select(g => (Alicuota: g.Key, Subtotal: g.Sum(l => l.Subtotal)))
            .OrderBy(g => g.Alicuota)
            .ToList();

        var subtotal = grupos.Sum(g => g.Subtotal);
        var total = subtotal - descuento;

        // La factura C no discrimina IVA: todo es "neto"
        if (tipo == TipoComprobante.FacturaC)
            return new ImportesFactura(total, 0, 0, total, []);

        var neto = 0m;
        var exento = 0m;
        var alicuotas = new List<FacturaIva>();
        var descuentoRepartido = 0m;

        for (var i = 0; i < grupos.Count; i++)
        {
            var (alicuota, subtotalGrupo) = grupos[i];

            var descuentoGrupo = i == grupos.Count - 1
                ? descuento - descuentoRepartido
                : subtotal == 0 ? 0 : Math.Round(descuento * subtotalGrupo / subtotal, 2, MidpointRounding.AwayFromZero);
            descuentoRepartido += descuentoGrupo;

            var totalGrupo = subtotalGrupo - descuentoGrupo;

            if (alicuota == AlicuotaIva.Exento)
            {
                exento += totalGrupo;
                continue;
            }

            var netoGrupo = Math.Round(totalGrupo / (1 + Tasa(alicuota)), 2, MidpointRounding.AwayFromZero);
            var ivaGrupo = totalGrupo - netoGrupo;

            neto += netoGrupo;
            alicuotas.Add(new FacturaIva { Alicuota = alicuota, BaseImponible = netoGrupo, Importe = ivaGrupo });
        }

        return new ImportesFactura(neto, exento, alicuotas.Sum(a => a.Importe), total, alicuotas);
    }
}
