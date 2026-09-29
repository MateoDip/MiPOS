namespace MiPOS.Dominio.Entidades;

public class Producto
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public bool Activo { get; set; } = true;

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
}