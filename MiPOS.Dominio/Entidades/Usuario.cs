namespace MiPOS.Dominio.Entidades;

public class Usuario
{
    public int Id { get; set; }

    // Con lo que se inicia sesión. Se guarda en minúsculas y sin espacios.
    public string NombreUsuario { get; set; } = string.Empty;

    // Lo que se muestra en pantalla (ej: "Juan Pérez")
    public string NombreCompleto { get; set; } = string.Empty;

    // Nunca la contraseña: solo su hash (ver MiPOS.Seguridad/HashContrasena)
    public string ContrasenaHash { get; set; } = string.Empty;

    public Rol Rol { get; set; }

    // Idioma de las pantallas para este usuario ("es", "zh"). null = el de la caja (Idioma:Predeterminado)
    public string? Idioma { get; set; }

    // Los usuarios no se borran: se desactivan
    public bool Activo { get; set; } = true;

    public DateTime FechaAlta { get; set; }

    // Protección contra adivinar contraseñas: intentos fallidos seguidos
    // y hasta cuándo está bloqueado (en hora UTC, igual en todas las cajas)
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
}
