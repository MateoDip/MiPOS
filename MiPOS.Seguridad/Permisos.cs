using MiPOS.Dominio.Entidades;

namespace MiPOS.Seguridad;

/// <summary>
/// Qué permisos tiene cada rol. Es el único lugar donde se define.
/// </summary>
public static class Permisos
{
    private static readonly Dictionary<Rol, HashSet<Permiso>> _permisosPorRol = new()
    {
        // El cajero solo vende. Para un descuento necesita que un administrador
        // escriba su contraseña (autorización), pero el permiso no es suyo.
        [Rol.Cajero] = new(),

        // El administrador puede todo
        [Rol.Administrador] = Enum.GetValues<Permiso>().ToHashSet()
    };

    public static bool Tiene(Rol rol, Permiso permiso)
        => _permisosPorRol.TryGetValue(rol, out var permisos) && permisos.Contains(permiso);
}
