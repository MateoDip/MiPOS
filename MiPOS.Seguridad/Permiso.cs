namespace MiPOS.Seguridad;

/// <summary>
/// Acciones que requieren un permiso especial.
/// Las tareas de venta (escanear, buscar, cambiar cantidad, eliminar, cancelar, cobrar)
/// las puede hacer cualquier usuario y no figuran acá.
/// Algunos permisos son para pantallas de fases futuras.
/// </summary>
public enum Permiso
{
    AplicarDescuento,
    GestionarUsuarios,
    GestionarProductos,
    ModificarPrecios,
    GestionarStock,
    GestionarCompras,
    GestionarCajas,
    RetirarEfectivo,
    ConfigurarHardware,
    ConfigurarFacturacion,
    ConfigurarSistema,
    CambiarIdioma,
    GestionarLicencia,

    // Abrir el cajón de dinero sin una venta (Ctrl+G)
    AbrirCajon
}
