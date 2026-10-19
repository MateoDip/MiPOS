using MiPOS.Dominio.Entidades;
using MiPOS.Facturacion;
using MiPOS.Negocio.Comun;
using MiPOS.Negocio.Facturacion;
using static MiPOS.Negocio.Comprobantes.FormatoTicket;

namespace MiPOS.Negocio.Comprobantes;

/// <summary>
/// Arma el texto del ticket de una venta, línea por línea, con el ancho exacto del papel.
/// Si la venta tiene factura electrónica, sale con el formato de factura (letra, número, CAE).
/// No sabe nada de impresoras: solo devuelve las líneas (el código QR va aparte).
/// </summary>
public static class TicketVenta
{
    // La venta tiene que venir con Detalles, MedioPago, Usuario, TurnoCaja.Caja y Factura.Alicuotas cargados
    public static IReadOnlyList<string> Armar(Venta venta, DatosComercio comercio, DatosFiscales fiscales,
                                              int ancho, bool esReimpresion)
    {
        var lineas = new List<string>();
        var separador = new string('-', ancho);
        var factura = venta.Factura;

        if (factura is null)
            EncabezadoComercio(lineas, comercio, ancho);
        else
            EncabezadoFiscal(lineas, comercio, fiscales, ancho);

        if (esReimpresion)
            Centrar(lineas, "*** REIMPRESIÓN ***", ancho);

        lineas.Add(separador);

        if (factura is not null)
            DatosFactura(lineas, factura, ancho, separador);

        DosColumnas(lineas, $"Venta N° {venta.Id}", venta.Fecha.ToString("dd/MM/yyyy HH:mm", Argentina), ancho);
        if (venta.TurnoCaja?.Caja is { } caja)
            AjustarTexto(lineas, caja.Nombre, ancho);
        AjustarTexto(lineas, $"Cajero: {venta.Usuario?.NombreCompleto}", ancho);
        lineas.Add(separador);

        // En la factura A los precios de las líneas son finales: se aclara, porque el IVA se discrimina abajo
        if (factura?.Tipo == TipoComprobante.FacturaA)
            AjustarTexto(lineas, "Precios unitarios con IVA incluido", ancho);

        // Productos: "2 x $ 2.500,00" y abajo el nombre con el subtotal a la derecha
        foreach (var detalle in venta.Detalles)
        {
            var iva = factura?.Tipo == TipoComprobante.FacturaA ? $"  (IVA {CalculadoraFactura.Nombre(detalle.AlicuotaIva)})" : string.Empty;
            AjustarTexto(lineas, $"{detalle.Cantidad} x {Dinero(detalle.PrecioUnitario)}{iva}", ancho);
            DosColumnas(lineas, detalle.NombreProducto, Dinero(detalle.Subtotal), ancho);
        }

        lineas.Add(separador);

        if (venta.Descuento > 0)
        {
            DosColumnas(lineas, "Subtotal", Dinero(venta.Subtotal), ancho);
            DosColumnas(lineas, $"Descuento {venta.DescuentoPorcentaje.ToString("0.##", Argentina)}%", $"-{Dinero(venta.Descuento)}", ancho);
        }

        // Factura A: neto e IVA por alícuota (discriminado)
        if (factura?.Tipo == TipoComprobante.FacturaA)
        {
            DosColumnas(lineas, "Neto gravado", Dinero(factura.ImporteNeto), ancho);
            foreach (var alicuota in factura.Alicuotas.OrderBy(a => a.Alicuota))
                DosColumnas(lineas, $"IVA {CalculadoraFactura.Nombre(alicuota.Alicuota)}", Dinero(alicuota.Importe), ancho);
            if (factura.ImporteExento > 0)
                DosColumnas(lineas, "Exento", Dinero(factura.ImporteExento), ancho);
        }

        DosColumnas(lineas, "TOTAL", Dinero(venta.Total), ancho);

        // Medio de pago: en efectivo, con cuánto pagó y el vuelto
        var medio = venta.MedioPago?.Nombre ?? string.Empty;
        DosColumnas(lineas, medio, Dinero(venta.MontoRecibido ?? venta.Total), ancho);
        if (venta.Vuelto is decimal vuelto)
            DosColumnas(lineas, "Vuelto", Dinero(vuelto), ancho);
        if (!string.IsNullOrEmpty(venta.ReferenciaPago))
            AjustarTexto(lineas, $"Cupón/Operación: {venta.ReferenciaPago}", ancho);

        // Factura B a consumidor final: Régimen de Transparencia Fiscal al Consumidor (Ley 27.743)
        if (factura?.Tipo == TipoComprobante.FacturaB)
        {
            lineas.Add(separador);
            AjustarTexto(lineas, "Régimen de Transparencia Fiscal al Consumidor (Ley 27.743)", ancho);
            DosColumnas(lineas, "IVA contenido", Dinero(factura.ImporteIva), ancho);
            DosColumnas(lineas, "Otros imp. nac. indirectos", Dinero(0), ancho);
        }

        lineas.Add(separador);
        Centrar(lineas, comercio.MensajePie, ancho);

        if (factura is null)
        {
            // Sin facturación electrónica el ticket nunca es una factura
            Centrar(lineas, "NO VÁLIDO COMO FACTURA", ancho);
        }
        else
        {
            PieFactura(lineas, factura, ancho);
        }

        return lineas;
    }

    // Razón social, domicilio, CUIT, IIBB, inicio de actividades y condición frente al IVA (obligatorios en la factura)
    private static void EncabezadoFiscal(List<string> lineas, DatosComercio comercio, DatosFiscales fiscales, int ancho)
    {
        var razonSocial = string.IsNullOrWhiteSpace(fiscales.RazonSocial) ? comercio.Nombre : fiscales.RazonSocial;
        Centrar(lineas, razonSocial.ToUpper(Argentina), ancho);

        // Si el nombre de fantasía es distinto de la razón social, también se muestra
        if (!string.IsNullOrWhiteSpace(fiscales.RazonSocial) && !string.Equals(comercio.Nombre.Trim(), fiscales.RazonSocial.Trim(), StringComparison.OrdinalIgnoreCase))
            Centrar(lineas, comercio.Nombre, ancho);

        Centrar(lineas, comercio.Direccion, ancho);

        var cuit = Cuit.IntentarNormalizar(comercio.Cuit, out var normalizado) ? Cuit.Formatear(normalizado) : comercio.Cuit.Trim();
        Centrar(lineas, $"CUIT {cuit}", ancho);

        if (!string.IsNullOrWhiteSpace(fiscales.IngresosBrutos))
            Centrar(lineas, $"IIBB {fiscales.IngresosBrutos.Trim()}", ancho);
        if (!string.IsNullOrWhiteSpace(fiscales.InicioActividades))
            Centrar(lineas, $"Inicio de actividades {fiscales.InicioActividades.Trim()}", ancho);

        Centrar(lineas, NombreCondicion(fiscales.CondicionIva), ancho);

        if (!string.IsNullOrWhiteSpace(comercio.Telefono))
            Centrar(lineas, $"Tel. {comercio.Telefono.Trim()}", ancho);
    }

    // Letra, número, fecha y cliente
    private static void DatosFactura(List<string> lineas, Factura factura, int ancho, string separador)
    {
        var tipo = CalculadoraFactura.NombreTipo(factura.Tipo);
        Centrar(lineas, $"{tipo}  (Cód. {(int)factura.Tipo:000})", ancho);

        if (factura.Estado == EstadoFactura.Autorizada)
        {
            DosColumnas(lineas, $"P.V. {factura.PuntoVenta:00000}", $"N° {factura.Numero:00000000}", ancho);
            DosColumnas(lineas, "Fecha", factura.Fecha?.ToString("dd/MM/yyyy", Argentina) ?? string.Empty, ancho);
        }
        else
        {
            Centrar(lineas, factura.Estado == EstadoFactura.Pendiente ? "PENDIENTE DE AUTORIZACIÓN" : "RECHAZADA POR ARCA", ancho);
        }

        // Cliente
        var receptor = new DatosReceptor(factura.ReceptorCondicionIva, factura.DocTipo, factura.DocNumero, factura.ReceptorNombre);
        if (receptor.Nombre is not null)
            AjustarTexto(lineas, $"Cliente: {receptor.Nombre}", ancho);
        AjustarTexto(lineas, receptor.EstaIdentificado ? receptor.DocumentoTexto : "Cliente: Consumidor final", ancho);
        if (receptor.CondicionIva != CondicionIva.ConsumidorFinal || receptor.EstaIdentificado)
            AjustarTexto(lineas, $"Cond. IVA: {NombreCondicion(receptor.CondicionIva)}", ancho);

        lineas.Add(separador);
    }

    // CAE y vencimiento, o la aclaración de que todavía no es una factura válida
    private static void PieFactura(List<string> lineas, Factura factura, int ancho)
    {
        if (factura.Estado == EstadoFactura.Autorizada)
        {
            DosColumnas(lineas, "CAE", factura.Cae ?? string.Empty, ancho);
            DosColumnas(lineas, "Vto. CAE", factura.VencimientoCae?.ToString("dd/MM/yyyy", Argentina) ?? string.Empty, ancho);
            Centrar(lineas, "Comprobante autorizado por ARCA", ancho);
        }
        else
        {
            Centrar(lineas, "NO VÁLIDO COMO FACTURA", ancho);
            Centrar(lineas, factura.Estado == EstadoFactura.Pendiente
                ? "La factura se emitirá cuando haya conexión con ARCA."
                : "La factura fue rechazada por ARCA.", ancho);
        }

        // Los comprobantes de prueba se marcan bien claro
        if (factura.Entorno == nameof(ModoFacturacion.Simulada))
            Centrar(lineas, "*** SIMULADA - SIN VALIDEZ FISCAL ***", ancho);
        else if (factura.Entorno == nameof(ModoFacturacion.Homologacion))
            Centrar(lineas, "*** HOMOLOGACIÓN - SIN VALIDEZ FISCAL ***", ancho);
    }

    public static string NombreCondicion(CondicionIva condicion) => condicion switch
    {
        CondicionIva.ResponsableInscripto => "IVA Responsable Inscripto",
        CondicionIva.Monotributo => "Responsable Monotributo",
        CondicionIva.Exento => "IVA Exento",
        _ => "Consumidor Final"
    };
}
