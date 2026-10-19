using System.Text.Json;

namespace MiPOS.Facturacion.Arca;

/// <summary>
/// Imita a ARCA para desarrollar y probar sin certificado ni Internet (Facturacion:Modo = Simulada).
/// Numera los comprobantes, controla los importes como ARCA e inventa un CAE.
/// Lo que "autoriza" se guarda en un archivo, así la numeración sigue igual al reiniciar el programa.
/// Los comprobantes simulados NO tienen validez fiscal.
/// </summary>
public class ServicioArcaSimulado : IServicioArca
{
    // Un solo pedido a la vez (como si fuera un único servidor)
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private readonly OpcionesFacturacion _opciones;

    public ServicioArcaSimulado(OpcionesFacturacion opciones)
    {
        _opciones = opciones;
    }

    // Para probar los casos difíciles desde MiPOS.Pruebas:
    // sin conexión (no llega el pedido) y respuesta perdida (ARCA autoriza pero la respuesta no llega)
    public static bool SimularSinConexion { get; set; }
    public static bool SimularRespuestaPerdida { get; set; }

    private string RutaArchivo => Path.Combine(_opciones.RutaCarpetaDatos, "simulacion-arca.json");

    public async Task<long> ObtenerUltimoNumeroAsync(long cuitEmisor, int puntoVenta, int tipoComprobante,
                                                     CancellationToken cancelacion = default)
    {
        ExigirConexion();

        await Candado.WaitAsync(cancelacion);
        try
        {
            return Leer().Comprobantes.Values
                .Where(c => c.Cuit == cuitEmisor && c.PuntoVenta == puntoVenta && c.Tipo == tipoComprobante)
                .Select(c => c.Numero)
                .DefaultIfEmpty(0)
                .Max();
        }
        finally
        {
            Candado.Release();
        }
    }

    public async Task<RespuestaCae> SolicitarCaeAsync(long cuitEmisor, SolicitudCae s, CancellationToken cancelacion = default)
    {
        ExigirConexion();

        await Candado.WaitAsync(cancelacion);
        try
        {
            var datos = Leer();
            var ultimo = datos.Comprobantes.Values
                .Where(c => c.Cuit == cuitEmisor && c.PuntoVenta == s.PuntoVenta && c.Tipo == s.TipoComprobante)
                .Select(c => c.Numero)
                .DefaultIfEmpty(0)
                .Max();

            // Los mismos controles que hace ARCA (con sus códigos de error)
            if (s.Numero != ultimo + 1)
                return Rechazo(10016, $"El número de comprobante debe ser el siguiente al último autorizado ({ultimo + 1}).");

            if (s.ImporteTotal != s.ImporteNeto + s.ImporteExento + s.ImporteIva)
                return Rechazo(10048, "El importe total no es igual a la suma de neto, exento e IVA.");

            if (s.Alicuotas.Sum(a => a.Importe) != s.ImporteIva)
                return Rechazo(10023, "La suma de los importes de IVA no coincide con el IVA del comprobante.");

            if (s.TipoComprobante == 11 && (s.ImporteIva != 0 || s.Alicuotas.Count > 0))
                return Rechazo(10071, "La factura C no discrimina IVA.");

            if (s.TipoComprobante == 1 && s.DocTipo != 80)
                return Rechazo(10013, "La factura A requiere el CUIT del receptor.");

            var cae = string.Concat(Enumerable.Range(0, 14).Select(_ => Random.Shared.Next(10).ToString()));
            var comprobante = new ComprobanteGuardado
            {
                Cuit = cuitEmisor,
                PuntoVenta = s.PuntoVenta,
                Tipo = s.TipoComprobante,
                Numero = s.Numero,
                Cae = cae,
                Vencimiento = s.Fecha.AddDays(10),
                Fecha = s.Fecha,
                ImporteTotal = s.ImporteTotal,
                DocNumero = s.DocNumero
            };
            datos.Comprobantes[$"{cuitEmisor}-{s.PuntoVenta}-{s.TipoComprobante}-{s.Numero}"] = comprobante;
            Guardar(datos);

            if (SimularRespuestaPerdida)
                throw new FacturacionException("ARCA no respondió (simulado: la respuesta se perdió después de autorizar).", reintentable: true);

            return new RespuestaCae(true, cae, comprobante.Vencimiento, string.Empty, []);
        }
        finally
        {
            Candado.Release();
        }
    }

    public async Task<ComprobanteConsultado?> ConsultarAsync(long cuitEmisor, int puntoVenta, int tipoComprobante, long numero,
                                                             CancellationToken cancelacion = default)
    {
        ExigirConexion();

        await Candado.WaitAsync(cancelacion);
        try
        {
            return Leer().Comprobantes.TryGetValue($"{cuitEmisor}-{puntoVenta}-{tipoComprobante}-{numero}", out var c)
                ? new ComprobanteConsultado(c.Numero, c.Cae, c.Vencimiento, c.Fecha, c.ImporteTotal, c.DocNumero)
                : null;
        }
        finally
        {
            Candado.Release();
        }
    }

    // ----- Ayudas -----

    private static void ExigirConexion()
    {
        if (SimularSinConexion)
            throw new FacturacionException("No hay conexión con ARCA (simulado).", reintentable: true);
    }

    private static RespuestaCae Rechazo(int codigo, string mensaje)
        => new(false, null, null, $"{codigo}: {mensaje}", [codigo]);

    private DatosSimulacion Leer()
    {
        if (!File.Exists(RutaArchivo))
            return new DatosSimulacion();

        return JsonSerializer.Deserialize<DatosSimulacion>(File.ReadAllText(RutaArchivo)) ?? new DatosSimulacion();
    }

    private void Guardar(DatosSimulacion datos)
    {
        Directory.CreateDirectory(_opciones.RutaCarpetaDatos);
        File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true }));
    }

    private class DatosSimulacion
    {
        public Dictionary<string, ComprobanteGuardado> Comprobantes { get; set; } = new();
    }

    private class ComprobanteGuardado
    {
        public long Cuit { get; set; }
        public int PuntoVenta { get; set; }
        public int Tipo { get; set; }
        public long Numero { get; set; }
        public string Cae { get; set; } = string.Empty;
        public DateOnly Vencimiento { get; set; }
        public DateOnly Fecha { get; set; }
        public decimal ImporteTotal { get; set; }
        public long DocNumero { get; set; }
    }
}
