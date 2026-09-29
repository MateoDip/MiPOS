namespace MiPOS.Dominio.Entidades;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    // Una categoría tiene muchos productos
    public List<Producto> Productos { get; set; } = new();
}