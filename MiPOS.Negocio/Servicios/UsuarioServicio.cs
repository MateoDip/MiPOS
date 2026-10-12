using Microsoft.EntityFrameworkCore;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Usuarios;
using MiPOS.Seguridad;

namespace MiPOS.Negocio.Servicios;

public class UsuarioServicio
{
    public const int LargoMinimoContrasena = 6;

    private const int IntentosPermitidos = 5;
    private static readonly TimeSpan TiempoBloqueo = TimeSpan.FromSeconds(30);

    // Mismo mensaje si el usuario no existe o si la contraseña es incorrecta:
    // así no se le dice a un intruso qué nombres de usuario existen
    private const string MensajeCredencialesIncorrectas = "Usuario o contraseña incorrectos.";

    // Hash de una contraseña que nadie conoce. Cuando el usuario no existe se verifica contra él,
    // para que la respuesta tarde lo mismo que cuando existe.
    private static readonly Lazy<string> _hashFicticio = new(() => HashContrasena.Generar(Guid.NewGuid().ToString()));

    private readonly IDbContextFactory<MiPOSDbContext> _fabrica;
    private readonly Sesion _sesion;

    public UsuarioServicio(IDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    // ----- Inicio de sesión -----

    public async Task<bool> ExisteAlgunUsuarioAsync()
    {
        await using var contexto = await _fabrica.CreateDbContextAsync();
        return await contexto.Usuarios.AnyAsync();
    }

    // Solo funciona si todavía no hay ningún usuario (primer arranque). Deja la sesión iniciada.
    public async Task<Usuario> CrearPrimerAdministradorAsync(string nombreUsuario, string nombreCompleto, string contrasena)
    {
        nombreUsuario = NormalizarNombreUsuario(nombreUsuario);
        nombreCompleto = nombreCompleto?.Trim() ?? string.Empty;
        ValidarNombreUsuario(nombreUsuario);
        ValidarNombreCompleto(nombreCompleto);
        ValidarContrasena(contrasena);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        if (await contexto.Usuarios.AnyAsync())
            throw new UsuarioInvalidoException("Ya existen usuarios. Inicie sesión.");

        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario,
            NombreCompleto = nombreCompleto,
            ContrasenaHash = await GenerarHashAsync(contrasena),
            Rol = Rol.Administrador,
            FechaAlta = DateTime.Now
        };

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();

        _sesion.Iniciar(usuario);
        return usuario;
    }

    public async Task<Usuario> IniciarSesionAsync(string nombreUsuario, string contrasena)
    {
        var usuario = await ValidarCredencialesAsync(nombreUsuario, contrasena);
        _sesion.Iniciar(usuario);
        return usuario;
    }

    public void CerrarSesion() => _sesion.Cerrar();

    /// <summary>
    /// Un usuario con el permiso escribe su usuario y contraseña para autorizar una acción
    /// (por ejemplo, un descuento que pide un cajero). Devuelve quién autorizó.
    /// No cambia la sesión actual.
    /// </summary>
    public async Task<Usuario> AutorizarAsync(string nombreUsuario, string contrasena, Permiso permiso)
    {
        var usuario = await ValidarCredencialesAsync(nombreUsuario, contrasena);

        if (!Permisos.Tiene(usuario.Rol, permiso))
            throw new UsuarioInvalidoException("Ese usuario no tiene permiso para autorizar esta acción.");

        return usuario;
    }

    private async Task<Usuario> ValidarCredencialesAsync(string nombreUsuario, string contrasena)
    {
        nombreUsuario = NormalizarNombreUsuario(nombreUsuario);

        if (nombreUsuario.Length == 0 || string.IsNullOrEmpty(contrasena))
            throw new UsuarioInvalidoException("Ingrese usuario y contraseña.");

        await using var contexto = await _fabrica.CreateDbContextAsync();

        var usuario = await contexto.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

        if (usuario is null)
        {
            await VerificarAsync(contrasena, _hashFicticio.Value);
            throw new UsuarioInvalidoException(MensajeCredencialesIncorrectas);
        }

        var ahora = DateTime.UtcNow;

        if (usuario.BloqueadoHastaUtc > ahora)
        {
            var segundos = Math.Ceiling((usuario.BloqueadoHastaUtc.Value - ahora).TotalSeconds);
            throw new UsuarioInvalidoException($"Usuario bloqueado por demasiados intentos fallidos. Espere {segundos} segundos.");
        }

        if (!await VerificarAsync(contrasena, usuario.ContrasenaHash))
        {
            usuario.IntentosFallidos++;

            if (usuario.IntentosFallidos >= IntentosPermitidos)
            {
                usuario.IntentosFallidos = 0;
                usuario.BloqueadoHastaUtc = ahora + TiempoBloqueo;
                await contexto.SaveChangesAsync();
                throw new UsuarioInvalidoException(
                    $"Demasiados intentos fallidos. El usuario queda bloqueado por {TiempoBloqueo.TotalSeconds} segundos.");
            }

            await contexto.SaveChangesAsync();
            throw new UsuarioInvalidoException(MensajeCredencialesIncorrectas);
        }

        // Recién con la contraseña correcta se informa que está desactivado
        if (!usuario.Activo)
            throw new UsuarioInvalidoException("El usuario está desactivado. Consulte con un administrador.");

        // Contraseña correcta: se reinicia el contador de intentos
        if (usuario.IntentosFallidos != 0 || usuario.BloqueadoHastaUtc is not null)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHastaUtc = null;
            await contexto.SaveChangesAsync();
        }

        return usuario;
    }

    // ----- Gestión de usuarios (solo administrador) -----

    public async Task<List<Usuario>> ObtenerTodosAsync()
    {
        _sesion.Exigir(Permiso.GestionarUsuarios);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        return await contexto.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync();
    }

    public async Task<Usuario> CrearAsync(string nombreUsuario, string nombreCompleto, string contrasena, Rol rol)
    {
        _sesion.Exigir(Permiso.GestionarUsuarios);

        nombreUsuario = NormalizarNombreUsuario(nombreUsuario);
        nombreCompleto = nombreCompleto?.Trim() ?? string.Empty;
        ValidarNombreUsuario(nombreUsuario);
        ValidarNombreCompleto(nombreCompleto);
        ValidarContrasena(contrasena);
        ValidarRol(rol);

        await using var contexto = await _fabrica.CreateDbContextAsync();

        if (await contexto.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario))
            throw new UsuarioInvalidoException($"Ya existe un usuario con el nombre \"{nombreUsuario}\".");

        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario,
            NombreCompleto = nombreCompleto,
            ContrasenaHash = await GenerarHashAsync(contrasena),
            Rol = rol,
            FechaAlta = DateTime.Now
        };

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();
        return usuario;
    }

    // El nombre de usuario no se puede cambiar (es con lo que se identifica en las ventas)
    public async Task ModificarAsync(int id, string nombreCompleto, Rol rol)
    {
        _sesion.Exigir(Permiso.GestionarUsuarios);
        var actual = _sesion.ExigirUsuario();

        nombreCompleto = nombreCompleto?.Trim() ?? string.Empty;
        ValidarNombreCompleto(nombreCompleto);
        ValidarRol(rol);

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var usuario = await BuscarAsync(contexto, id);

        if (usuario.Rol == Rol.Administrador && rol != Rol.Administrador)
        {
            if (usuario.Id == actual.Id)
                throw new UsuarioInvalidoException("No puede quitarse a sí mismo el rol de administrador.");

            if (usuario.Activo)
                await VerificarQueQuedeOtroAdministradorAsync(contexto, usuario.Id);
        }

        usuario.NombreCompleto = nombreCompleto;
        usuario.Rol = rol;
        await contexto.SaveChangesAsync();

        // Si se modificó a sí mismo, se actualiza el nombre que muestra la pantalla
        if (usuario.Id == actual.Id)
            actual.NombreCompleto = nombreCompleto;
    }

    public async Task CambiarActivoAsync(int id, bool activo)
    {
        _sesion.Exigir(Permiso.GestionarUsuarios);
        var actual = _sesion.ExigirUsuario();

        if (!activo && id == actual.Id)
            throw new UsuarioInvalidoException("No puede desactivarse a sí mismo.");

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var usuario = await BuscarAsync(contexto, id);

        if (!activo && usuario.Rol == Rol.Administrador && usuario.Activo)
            await VerificarQueQuedeOtroAdministradorAsync(contexto, usuario.Id);

        usuario.Activo = activo;
        await contexto.SaveChangesAsync();
    }

    // Para cuando un usuario se olvidó la contraseña. También lo desbloquea.
    public async Task RestablecerContrasenaAsync(int id, string nuevaContrasena)
    {
        _sesion.Exigir(Permiso.GestionarUsuarios);
        ValidarContrasena(nuevaContrasena);

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var usuario = await BuscarAsync(contexto, id);

        usuario.ContrasenaHash = await GenerarHashAsync(nuevaContrasena);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHastaUtc = null;
        await contexto.SaveChangesAsync();
    }

    // ----- Cualquier usuario -----

    public async Task CambiarMiContrasenaAsync(string contrasenaActual, string nuevaContrasena)
    {
        var actual = _sesion.ExigirUsuario();
        ValidarContrasena(nuevaContrasena);

        if (contrasenaActual == nuevaContrasena)
            throw new UsuarioInvalidoException("La nueva contraseña debe ser distinta de la actual.");

        await using var contexto = await _fabrica.CreateDbContextAsync();
        var usuario = await BuscarAsync(contexto, actual.Id);

        if (!await VerificarAsync(contrasenaActual, usuario.ContrasenaHash))
            throw new UsuarioInvalidoException("La contraseña actual es incorrecta.");

        usuario.ContrasenaHash = await GenerarHashAsync(nuevaContrasena);
        await contexto.SaveChangesAsync();
    }

    // ----- Reglas y ayudas -----

    private static async Task<Usuario> BuscarAsync(MiPOSDbContext contexto, int id)
        => await contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == id)
           ?? throw new UsuarioInvalidoException("El usuario no existe.");

    private static async Task VerificarQueQuedeOtroAdministradorAsync(MiPOSDbContext contexto, int idQueDejaDeSerlo)
    {
        var hayOtro = await contexto.Usuarios.AnyAsync(u =>
            u.Id != idQueDejaDeSerlo && u.Activo && u.Rol == Rol.Administrador);

        if (!hayOtro)
            throw new UsuarioInvalidoException("Debe quedar al menos un administrador activo.");
    }

    private static string NormalizarNombreUsuario(string? nombreUsuario)
        => (nombreUsuario ?? string.Empty).Trim().ToLowerInvariant();

    private static void ValidarNombreUsuario(string nombreUsuario)
    {
        if (nombreUsuario.Length < 3 || nombreUsuario.Length > 50)
            throw new UsuarioInvalidoException("El nombre de usuario debe tener entre 3 y 50 caracteres.");

        if (!nombreUsuario.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new UsuarioInvalidoException("El nombre de usuario solo puede tener letras, números, punto, guion y guion bajo (sin espacios).");
    }

    private static void ValidarNombreCompleto(string nombreCompleto)
    {
        if (nombreCompleto.Length == 0)
            throw new UsuarioInvalidoException("Ingrese el nombre completo.");

        if (nombreCompleto.Length > 100)
            throw new UsuarioInvalidoException("El nombre completo no puede tener más de 100 caracteres.");
    }

    private static void ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrWhiteSpace(contrasena) || contrasena.Length < LargoMinimoContrasena)
            throw new UsuarioInvalidoException($"La contraseña debe tener al menos {LargoMinimoContrasena} caracteres.");
    }

    private static void ValidarRol(Rol rol)
    {
        if (!Enum.IsDefined(rol))
            throw new UsuarioInvalidoException("Rol inválido.");
    }

    // El hash tarda: se calcula fuera del hilo de la pantalla para que no se congele
    private static Task<string> GenerarHashAsync(string contrasena)
        => Task.Run(() => HashContrasena.Generar(contrasena));

    private static Task<bool> VerificarAsync(string contrasena, string hash)
        => Task.Run(() => HashContrasena.Verificar(contrasena, hash));
}
