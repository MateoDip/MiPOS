using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiPOS.Hardware.Terminal;

/// <summary>
/// Cobro integrado con Mercado Pago Point usando la API de órdenes (Orders API):
/// se crea una orden para la terminal y se consulta su estado hasta que termina.
/// Documentación: https://www.mercadopago.com.ar/developers/es/reference (Pagos presenciales → Point).
/// </summary>
public class TerminalMercadoPagoPoint : ITerminalPago
{
    private const string UrlOrdenes = "https://api.mercadopago.com/v1/orders";

    // Cada cuánto se consulta si el cliente ya pagó
    private static readonly TimeSpan IntervaloConsulta = TimeSpan.FromSeconds(2);

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly OpcionesTerminal _opciones;

    public TerminalMercadoPagoPoint(OpcionesTerminal opciones)
    {
        _opciones = opciones;
    }

    public async Task<ResultadoTerminal> CobrarAsync(decimal monto, string referencia, string descripcion,
                                                     IProgress<string>? progreso, CancellationToken cancelacion)
    {
        _opciones.Validar();

        progreso?.Report("Enviando el monto a la terminal...");
        var orden = await EnviarAsync(HttpMethod.Post, UrlOrdenes, ArmarOrden(monto, referencia, descripcion, _opciones.TerminalId),
            claveIdempotencia: referencia, CancellationToken.None);
        var idOrden = orden["id"]?.GetValue<string>()
                      ?? throw new HardwareException("Mercado Pago no devolvió el número de la orden.");

        var cancelacionPedida = false;

        // Se consulta hasta que la orden termine. Aunque el cajero cancele, se sigue consultando:
        // si el cliente ya estaba pagando, hay que saber cómo terminó.
        while (true)
        {
            if (cancelacion.IsCancellationRequested && !cancelacionPedida)
            {
                cancelacionPedida = true;
                progreso?.Report("Cancelando el cobro...");
                await IntentarCancelarAsync(idOrden, progreso);
            }

            try
            {
                await Task.Delay(IntervaloConsulta, cancelacionPedida ? CancellationToken.None : cancelacion);
            }
            catch (OperationCanceledException)
            {
                continue; // vuelve arriba para pedir la cancelación
            }

            JsonNode estado;
            try
            {
                estado = await EnviarAsync(HttpMethod.Get, $"{UrlOrdenes}/{idOrden}", null, null, CancellationToken.None);
            }
            catch (HardwareException ex)
            {
                // Un corte momentáneo no termina el cobro: se avisa y se sigue consultando
                progreso?.Report($"Sin respuesta de Mercado Pago ({ex.Message}). Reintentando...");
                continue;
            }

            var resultado = Interpretar(estado);
            if (resultado is not null)
                return resultado;

            if (!cancelacionPedida)
                progreso?.Report(estado["status"]?.GetValue<string>() == "at_terminal"
                    ? $"El cliente está pagando $ {monto:N2} en la terminal... (Esc para cancelar)"
                    : $"Esperando que la terminal tome el cobro de $ {monto:N2}... (Esc para cancelar)");
        }
    }

    /// <summary>
    /// El pedido de orden para la terminal. El monto va como texto con 2 decimales y punto ("2500.00").
    /// Público para poder probarlo.
    /// </summary>
    public static JsonObject ArmarOrden(decimal monto, string referencia, string descripcion, string terminalId)
        => new()
        {
            ["type"] = "point",
            ["external_reference"] = referencia,
            ["description"] = descripcion,
            ["transactions"] = new JsonObject
            {
                ["payments"] = new JsonArray(new JsonObject
                {
                    ["amount"] = monto.ToString("0.00", CultureInfo.InvariantCulture)
                })
            },
            ["config"] = new JsonObject
            {
                ["point"] = new JsonObject { ["terminal_id"] = terminalId }
            }
        };

    /// <summary>
    /// Lee el estado de la orden. null = todavía no terminó.
    /// Público para poder probarlo.
    /// </summary>
    public static ResultadoTerminal? Interpretar(JsonNode orden)
    {
        var estado = orden["status"]?.GetValue<string>();
        var detalle = orden["status_detail"]?.GetValue<string>();
        var idPago = orden["transactions"]?["payments"]?[0]?["id"]?.GetValue<string>();

        return estado switch
        {
            "processed" => new ResultadoTerminal(true, idPago ?? orden["id"]?.GetValue<string>(), "Pago aprobado."),
            "canceled" => new ResultadoTerminal(false, null, detalle == "canceled_on_terminal"
                ? "El cobro se canceló en la terminal."
                : "Cobro cancelado."),
            "expired" => new ResultadoTerminal(false, null, "El cobro venció sin que el cliente pagara."),
            "failed" => new ResultadoTerminal(false, null, "El pago fue rechazado."),
            "action_required" => new ResultadoTerminal(false, null,
                "Mercado Pago pide revisar el pago en la terminal. Confirme allí si se cobró antes de entregar la mercadería."),
            _ => null // created, at_terminal: sigue esperando
        };
    }

    // Solo se puede cancelar por la API mientras la terminal no la tomó (estado "created")
    private async Task IntentarCancelarAsync(string idOrden, IProgress<string>? progreso)
    {
        try
        {
            await EnviarAsync(HttpMethod.Post, $"{UrlOrdenes}/{idOrden}/cancel", null, Guid.NewGuid().ToString(), CancellationToken.None);
        }
        catch (HardwareException)
        {
            progreso?.Report("El cliente ya está pagando: cancele la operación en la terminal. Esperando el resultado...");
        }
    }

    private async Task<JsonNode> EnviarAsync(HttpMethod metodo, string url, JsonObject? cuerpo, string? claveIdempotencia,
                                             CancellationToken cancelacion)
    {
        using var pedido = new HttpRequestMessage(metodo, url);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opciones.AccessToken);
        if (claveIdempotencia is not null)
            pedido.Headers.Add("X-Idempotency-Key", claveIdempotencia);
        if (cuerpo is not null)
            pedido.Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");
        else if (metodo == HttpMethod.Post)
            pedido.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(_opciones.TiempoEsperaSegundos));

        try
        {
            using var respuesta = await Http.SendAsync(pedido, limite.Token);
            var texto = await respuesta.Content.ReadAsStringAsync(limite.Token);

            if (!respuesta.IsSuccessStatusCode)
            {
                var mensaje = LeerMensajeError(texto);
                throw new HardwareException((int)respuesta.StatusCode switch
                {
                    401 => "Mercado Pago rechazó el Access Token (Terminal:AccessToken).",
                    403 => "La terminal no pertenece a la cuenta de Mercado Pago configurada (Terminal:TerminalId).",
                    _ => $"Mercado Pago respondió con un error ({(int)respuesta.StatusCode}): {mensaje}"
                });
            }

            return JsonNode.Parse(texto) ?? throw new HardwareException("Mercado Pago devolvió una respuesta vacía.");
        }
        catch (OperationCanceledException ex) when (!cancelacion.IsCancellationRequested)
        {
            throw new HardwareException("Mercado Pago no respondió a tiempo.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new HardwareException("No hay conexión con Mercado Pago. Verifique la conexión a Internet.", ex);
        }
        catch (JsonException ex)
        {
            throw new HardwareException("La respuesta de Mercado Pago no se pudo leer.", ex);
        }
    }

    private static string LeerMensajeError(string texto)
    {
        try
        {
            var json = JsonNode.Parse(texto);
            return json?["message"]?.GetValue<string>()
                   ?? json?["errors"]?[0]?["message"]?.GetValue<string>()
                   ?? texto;
        }
        catch (JsonException)
        {
            return texto;
        }
    }
}
