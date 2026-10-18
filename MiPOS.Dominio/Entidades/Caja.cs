namespace MiPOS.Dominio.Entidades;

/// <summary>
/// Un puesto de cobro (una PC). Cada PC indica cuál es en appsettings.json (Caja:Numero).
/// </summary>
public class Caja
{
    public int Id { get; set; }

    // Número que se configura en cada PC y se imprime en los tickets (ej: 1, 2, 3)
    public int Numero { get; set; }

    // Lo que se muestra en pantalla (ej: "Caja 1", "Caja rápida")
    public string Nombre { get; set; } = string.Empty;

    // Las cajas no se borran: se desactivan
    public bool Activa { get; set; } = true;
}
