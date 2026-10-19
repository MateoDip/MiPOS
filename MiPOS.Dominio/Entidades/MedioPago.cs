namespace MiPOS.Dominio.Entidades;

public class MedioPago
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Solo el efectivo pide "Paga con" y calcula vuelto.
    // Se usa esta marca en vez de comparar el nombre (que cambiará con el idioma).
    public bool EsEfectivo { get; set; }

    // Se cobra con la terminal de pago (posnet o Mercado Pago Point): débito, crédito, etc.
    public bool UsaTerminal { get; set; }

    public bool Activo { get; set; } = true;
}
