using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.DatosLocal;
using MiPOS.Dominio.Entidades;

namespace MiPOS.Negocio.SinConexion;

/// <summary>
/// La base local de esta caja y si hay conexión con la base central.
/// Es Singleton (como Sesion): toda la aplicación tiene que saber al mismo tiempo que se cortó la conexión,
/// para no esperar el tiempo de conexión en cada producto escaneado.
/// Cuando un servicio detecta un corte (EsCorte), la caja pasa a trabajar con la base local
/// hasta que SincronizacionServicio vuelve a conectarse.
/// </summary>
public class BaseLocal
{
    // Claves de los datos sueltos guardados en la tabla Valores
    internal const string ClaveCaja = "Caja";
    internal const string ClaveTurnoAbierto = "TurnoAbierto";
    internal const string ClaveUltimaDescarga = "UltimaDescarga";

    private readonly IDbContextFactory<LocalDbContext> _fabrica;
    private volatile bool _preparada;
    private volatile bool _sinConexion;

    public BaseLocal(IDbContextFactory<LocalDbContext> fabrica, OpcionesSinConexion opciones)
    {
        _fabrica = fabrica;
        Opciones = opciones;
    }

    public OpcionesSinConexion Opciones { get; }

    // Se puede vender sin conexión: el modo está activo y la base local se pudo crear
    public bool Habilitada => Opciones.Activo && _preparada;

    // Si se pudo preparar la base local, por qué no (para la pantalla de prueba)
    public string? ErrorPreparacion { get; private set; }

    public bool SinConexion => _sinConexion;

    public DateTime? SinConexionDesde { get; private set; }

    // true = las búsquedas y las ventas van directo a la base local
    public bool UsarLocal => Habilitada && _sinConexion;

    // Crea la base local si hace falta. Se llama una vez al arrancar la aplicación.
    // Si falla (disco lleno, sin permisos), la caja funciona igual, pero solo con conexión.
    public void Preparar()
    {
        if (!Opciones.Activo)
            return;

        try
        {
            var carpeta = Path.GetDirectoryName(Opciones.RutaCompleta);
            if (!string.IsNullOrEmpty(carpeta))
                Directory.CreateDirectory(carpeta);

            using var contexto = _fabrica.CreateDbContext();
            contexto.Preparar();
            _preparada = true;
            ErrorPreparacion = null;
        }
        catch (Exception ex)
        {
            _preparada = false;
            ErrorPreparacion = ex.Message;
        }
    }

    public Task<LocalDbContext> CrearContextoAsync() => _fabrica.CreateDbContextAsync();

    /// <summary>
    /// true si el error es un corte de conexión con la base central y se puede seguir con la base local.
    /// En ese caso, desde ahora la caja trabaja sin conexión. Se usa en los catch: "catch (Exception ex) when (EsCorte(ex))".
    /// </summary>
    public bool EsCorte(Exception error)
    {
        if (!Habilitada || !ErroresConexion.EsErrorDeConexion(error))
            return false;

        MarcarSinConexion();
        return true;
    }

    // ----- Caja y turno de esta PC (para seguir vendiendo en el mismo turno sin conexión) -----

    internal async Task<Caja?> LeerCajaAsync()
    {
        await using var local = await CrearContextoAsync();
        return await local.LeerAsync<Caja>(ClaveCaja);
    }

    internal async Task<TurnoCaja?> LeerTurnoAsync()
    {
        await using var local = await CrearContextoAsync();
        return await local.LeerAsync<TurnoCaja>(ClaveTurnoAbierto);
    }

    // Guarda la caja y su turno abierto (null = caja cerrada). Si falla, no se interrumpe nada:
    // la próxima sincronización lo vuelve a guardar.
    internal async Task GuardarCajaYTurnoAsync(Caja caja, TurnoCaja? turno)
    {
        if (!Habilitada)
            return;

        try
        {
            await using var local = await CrearContextoAsync();
            await local.GuardarAsync(ClaveCaja, new Caja { Id = caja.Id, Numero = caja.Numero, Nombre = caja.Nombre, Activa = caja.Activa });

            // Sin los usuarios ni los movimientos: solo lo que hace falta para vender
            await local.GuardarAsync(ClaveTurnoAbierto, turno is null ? null : new TurnoCaja
            {
                Id = turno.Id,
                CajaId = turno.CajaId,
                FechaApertura = turno.FechaApertura,
                MontoInicial = turno.MontoInicial,
                UsuarioAperturaId = turno.UsuarioAperturaId
            });
            await local.SaveChangesAsync();
        }
        catch (Exception)
        {
        }
    }

    // Una venta hecha sin conexión que todavía no se subió, lista para armar su ticket. null si ya se subió.
    internal async Task<Venta?> LeerVentaPendienteAsync(Guid identificador)
    {
        if (!Habilitada)
            return null;

        await using var local = await CrearContextoAsync();
        var pendiente = await local.VentasPendientes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdentificadorUnico == identificador);
        if (pendiente is null)
            return null;

        var venta = System.Text.Json.JsonSerializer.Deserialize<Venta>(pendiente.Datos, ValorLocal.OpcionesJson)!;
        venta.Id = pendiente.Id;
        venta.RegistradaSinConexion = true;
        venta.MedioPago = await local.MediosPago.AsNoTracking().FirstOrDefaultAsync(m => m.Id == venta.MedioPagoId);
        venta.Usuario = await local.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == venta.UsuarioId);

        var caja = await local.LeerAsync<Caja>(ClaveCaja);
        if (caja is not null)
            venta.TurnoCaja = new TurnoCaja { Id = venta.TurnoCajaId ?? 0, Caja = caja };

        return venta;
    }

    internal async Task<int> ContarVentasPendientesAsync()
    {
        if (!Habilitada)
            return 0;

        await using var local = await CrearContextoAsync();
        return await local.VentasPendientes.CountAsync();
    }

    internal void MarcarSinConexion()
    {
        if (_sinConexion)
            return;

        SinConexionDesde = DateTime.Now;
        _sinConexion = true;
    }

    internal void MarcarConConexion()
    {
        _sinConexion = false;
        SinConexionDesde = null;
    }
}
