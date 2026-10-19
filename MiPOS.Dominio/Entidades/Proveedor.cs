namespace MiPOS.Dominio.Entidades;

public class Proveedor
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    // Opcionales. El CUIT se guarda solo con números (11 dígitos).
    public string? Cuit { get; set; }
    public string? Telefono { get; set; }

    // Los proveedores no se borran: se desactivan
    public bool Activo { get; set; } = true;
}
