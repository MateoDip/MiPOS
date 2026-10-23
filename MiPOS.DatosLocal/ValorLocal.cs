using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace MiPOS.DatosLocal;

/// <summary>
/// Un dato suelto de la base local, guardado como texto (ej: el turno abierto de la caja,
/// la fecha de la última descarga del catálogo). Solo existe en la base local.
/// </summary>
public class ValorLocal
{
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;

    // Cómo se pasan los objetos a texto (JSON) en la base local. IgnoreCycles: las entidades
    // tienen referencias de ida y vuelta (la venta a sus detalles y cada detalle a la venta).
    public static readonly JsonSerializerOptions OpcionesJson = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
}

public static class ValoresLocales
{
    // Lee un objeto guardado con GuardarAsync. null si no existe o si quedó guardado vacío.
    public static async Task<T?> LeerAsync<T>(this LocalDbContext contexto, string clave) where T : class
    {
        var valor = await contexto.Valores.AsNoTracking().FirstOrDefaultAsync(v => v.Clave == clave);
        return string.IsNullOrEmpty(valor?.Valor) ? null : JsonSerializer.Deserialize<T>(valor.Valor, ValorLocal.OpcionesJson);
    }

    // Guarda (o reemplaza) un objeto como JSON. null lo deja vacío. Lo graba el SaveChanges de quien llama.
    public static async Task GuardarAsync<T>(this LocalDbContext contexto, string clave, T? objeto) where T : class
    {
        var texto = objeto is null ? string.Empty : JsonSerializer.Serialize(objeto, ValorLocal.OpcionesJson);
        var valor = await contexto.Valores.FirstOrDefaultAsync(v => v.Clave == clave);

        if (valor is null)
            contexto.Valores.Add(new ValorLocal { Clave = clave, Valor = texto });
        else
            valor.Valor = texto;
    }
}
