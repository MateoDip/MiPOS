using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.SinConexion;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

/// <summary>
/// Control de caja: apertura, retiros e ingresos de efectivo, y cierre con arqueo.
/// Trabaja siempre sobre la caja de esta PC (Caja:Numero en appsettings.json).
/// </summary>
public class CajaServicio
{
    public const int LargoMaximoMotivo = 200;

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;
    private readonly OpcionesCaja _opciones;
    private readonly BaseLocal? _baseLocal;

    public CajaServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion, OpcionesCaja opciones,
                        BaseLocal? baseLocal = null)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _opciones = opciones;
        _baseLocal = baseLocal;
    }

    // La caja de esta PC. Lanza CajaInvalidaException si no existe o está desactivada.
    // Sin conexión, la que quedó guardada en la base local.
    public async Task<Caja> ObtenerCajaAsync()
    {
        if (_baseLocal?.UsarLocal == true && await _baseLocal.LeerCajaAsync() is { } guardada)
            return guardada;

        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync();
            return await BuscarCajaAsync(contexto);
        }
        catch (Exception ex) when (_baseLocal?.EsCorte(ex) == true)
        {
            return await _baseLocal.LeerCajaAsync()
                   ?? throw new CajaInvalidaException("No hay conexión con la base central y esta caja todavía no tiene sus datos guardados.");
        }
    }

    // El turno abierto de la caja de esta PC, o null si la caja está cerrada.
    // Sin conexión, el que estaba abierto la última vez que hubo conexión.
    public async Task<TurnoCaja?> ObtenerTurnoAbiertoAsync()
    {
        if (_baseLocal?.UsarLocal == true)
            return await LeerTurnoLocalAsync();

        try
        {
            await using var contexto = await _fabrica.CreateDbContextAsync();

            var turno = await contexto.TurnosCaja
                .AsNoTracking()
                .Include(t => t.Caja)
                .Include(t => t.UsuarioApertura)
                .FirstOrDefaultAsync(t => t.Caja!.Numero == _opciones.Numero && t.FechaCierre == null);

            if (turno?.Caja is { } caja)
                await (_baseLocal?.GuardarCajaYTurnoAsync(caja, turno) ?? Task.CompletedTask);

            return turno;
        }
        catch (Exception ex) when (_baseLocal?.EsCorte(ex) == true)
        {
            return await LeerTurnoLocalAsync();
        }
    }

    private async Task<TurnoCaja?> LeerTurnoLocalAsync()
    {
        var turno = await _baseLocal!.LeerTurnoAsync();
        if (turno is not null)
            turno.Caja = await _baseLocal.LeerCajaAsync();
        return turno;
    }

    public async Task<TurnoCaja> AbrirTurnoAsync(decimal montoInicial)
    {
        var usuario = _sesion.ExigirUsuario();
        ValidarMonto(montoInicial, permitirCero: true);

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var caja = await BuscarCajaAsync(contexto);

        if (await contexto.TurnosCaja.AnyAsync(t => t.CajaId == caja.Id && t.FechaCierre == null))
            throw new CajaInvalidaException($"{caja.Nombre} ya está abierta.");

        var turno = new TurnoCaja
        {
            CajaId = caja.Id,
            UsuarioAperturaId = usuario.Id,
            FechaApertura = DateTime.Now,
            MontoInicial = montoInicial
        };

        contexto.TurnosCaja.Add(turno);

        try
        {
            await contexto.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // La base rechaza un segundo turno abierto (índice único): otra PC la abrió recién
            throw new CajaInvalidaException($"{caja.Nombre} acaba de ser abierta desde otra PC.");
        }

        turno.Caja = caja;
        turno.UsuarioApertura = usuario;

        // Si después se corta la conexión, se sigue vendiendo en este turno
        await (_baseLocal?.GuardarCajaYTurnoAsync(caja, turno) ?? Task.CompletedTask);
        return turno;
    }

    /// <summary>
    /// Registra un ingreso o un retiro de efectivo en el turno abierto.
    /// El retiro necesita quién lo autorizó (autorizadoPorId); se vuelve a verificar en la base.
    /// </summary>
    public async Task<MovimientoCaja> RegistrarMovimientoAsync(TipoMovimientoCaja tipo, decimal monto,
                                                               string motivo, int? autorizadoPorId)
    {
        var usuario = _sesion.ExigirUsuario();

        if (!Enum.IsDefined(tipo))
            throw new CajaInvalidaException("Tipo de movimiento inválido.");

        ValidarMonto(monto, permitirCero: false);

        motivo = motivo?.Trim() ?? string.Empty;
        if (motivo.Length == 0)
            throw new CajaInvalidaException("Ingrese el motivo.");
        if (motivo.Length > LargoMaximoMotivo)
            throw new CajaInvalidaException($"El motivo no puede tener más de {LargoMaximoMotivo} caracteres.");

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var turno = await BuscarTurnoAbiertoAsync(contexto);

        if (tipo == TipoMovimientoCaja.Retiro)
        {
            // Igual que con los descuentos: no alcanza con lo que dice la pantalla
            var autorizante = await contexto.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == autorizadoPorId && u.Activo);

            if (autorizante is null || !Permisos.Tiene(autorizante.Rol, Permiso.RetirarEfectivo))
                throw new PermisoDenegadoException("El retiro de efectivo requiere autorización de un administrador.");

            var resumen = await CalcularResumenAsync(contexto, turno.Id);
            if (monto > resumen.EfectivoEsperado)
                throw new CajaInvalidaException(
                    $"No se puede retirar más del efectivo que debería haber en la caja ($ {resumen.EfectivoEsperado:N2}).");
        }
        else
        {
            // Un ingreso no necesita autorización
            autorizadoPorId = null;
        }

        var movimiento = new MovimientoCaja
        {
            TurnoCajaId = turno.Id,
            Tipo = tipo,
            Monto = monto,
            Motivo = motivo,
            Fecha = DateTime.Now,
            UsuarioId = usuario.Id,
            AutorizadoPorId = autorizadoPorId
        };

        contexto.MovimientosCaja.Add(movimiento);
        await contexto.SaveChangesAsync();
        return movimiento;
    }

    /// <summary>
    /// Cierra el turno abierto con el efectivo que contó el cajero (arqueo ciego:
    /// el cajero no ve el esperado antes de contar). Devuelve el resumen con la diferencia.
    /// </summary>
    public async Task<ResumenTurno> CerrarTurnoAsync(decimal efectivoContado)
    {
        var usuario = _sesion.ExigirUsuario();
        ValidarMonto(efectivoContado, permitirCero: true);

        // Las ventas hechas sin conexión son parte del turno: sin ellas el arqueo daría mal
        var pendientes = await (_baseLocal?.ContarVentasPendientesAsync() ?? Task.FromResult(0));
        if (pendientes > 0)
            throw new CajaInvalidaException(
                $"Hay {pendientes} venta(s) hechas sin conexión que todavía no se subieron a la base central. " +
                "Se suben solas cuando vuelve la conexión; espere unos segundos y vuelva a intentar.");

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var turno = await BuscarTurnoAbiertoAsync(contexto);

        var esperado = (await CalcularResumenAsync(contexto, turno.Id)).EfectivoEsperado;
        var ahora = DateTime.Now;

        // Se cierra solo si sigue abierto: si otra PC lo cerró recién, no se pisa su arqueo
        var filas = await contexto.TurnosCaja
            .Where(t => t.Id == turno.Id && t.FechaCierre == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.FechaCierre, ahora)
                .SetProperty(t => t.UsuarioCierreId, usuario.Id)
                .SetProperty(t => t.EfectivoEsperado, esperado)
                .SetProperty(t => t.EfectivoContado, efectivoContado)
                .SetProperty(t => t.Diferencia, efectivoContado - esperado));

        if (filas == 0)
            throw new CajaInvalidaException("La caja acaba de ser cerrada desde otra PC.");

        // Sin turno abierto, sin conexión no se puede vender
        await (_baseLocal?.GuardarCajaYTurnoAsync(await BuscarCajaAsync(contexto), null) ?? Task.CompletedTask);

        return await CalcularResumenAsync(contexto, turno.Id);
    }

    // Resumen de cualquier turno (abierto o cerrado), por ejemplo para imprimir el cierre
    public async Task<ResumenTurno> ObtenerResumenAsync(int turnoId)
    {
        _sesion.ExigirUsuario();

        await using var contexto = await _fabrica.CreateDbContextAsync();
        return await CalcularResumenAsync(contexto, turnoId);
    }

    // ----- Ayudas -----

    private async Task<Caja> BuscarCajaAsync(MiPOSDbContext contexto)
        => await contexto.Cajas.AsNoTracking().FirstOrDefaultAsync(c => c.Numero == _opciones.Numero && c.Activa)
           ?? throw new CajaInvalidaException(
               $"La caja N° {_opciones.Numero} (Caja:Numero en appsettings.json) no existe o está desactivada.");

    private async Task<TurnoCaja> BuscarTurnoAbiertoAsync(MiPOSDbContext contexto)
        => await contexto.TurnosCaja
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.Caja!.Numero == _opciones.Numero && t.FechaCierre == null)
           ?? throw new CajaInvalidaException("La caja está cerrada. Ábrala con F11.");

    private static async Task<ResumenTurno> CalcularResumenAsync(MiPOSDbContext contexto, int turnoId)
    {
        var turno = await contexto.TurnosCaja
            .AsNoTracking()
            .Include(t => t.Caja)
            .Include(t => t.UsuarioApertura)
            .Include(t => t.UsuarioCierre)
            .Include(t => t.Movimientos.OrderBy(m => m.Id))
            .FirstOrDefaultAsync(t => t.Id == turnoId)
            ?? throw new CajaInvalidaException($"El turno N° {turnoId} no existe.");

        // Totales agrupados por medio de pago, calculados por la base
        var porMedio = await contexto.Ventas
            .Where(v => v.TurnoCajaId == turnoId)
            .GroupBy(v => new { v.MedioPagoId, v.MedioPago!.Nombre, v.MedioPago.EsEfectivo })
            .Select(g => new
            {
                g.Key.MedioPagoId,
                g.Key.Nombre,
                g.Key.EsEfectivo,
                Cantidad = g.Count(),
                Total = g.Sum(v => v.Total),
                Descuentos = g.Sum(v => v.Descuento)
            })
            .ToListAsync();

        return new ResumenTurno
        {
            Turno = turno,
            VentasPorMedio = porMedio
                .OrderBy(m => m.MedioPagoId)
                .Select(m => new VentasPorMedio(m.Nombre, m.EsEfectivo, m.Cantidad, m.Total))
                .ToList(),
            TotalDescuentos = porMedio.Sum(m => m.Descuentos)
        };
    }

    private static void ValidarMonto(decimal monto, bool permitirCero)
    {
        if (monto < 0 || (!permitirCero && monto == 0))
            throw new CajaInvalidaException(permitirCero
                ? "El monto no puede ser negativo."
                : "El monto tiene que ser mayor a cero.");

        if (decimal.Round(monto, 2) != monto)
            throw new CajaInvalidaException("El monto puede tener como máximo 2 decimales.");
    }
}
