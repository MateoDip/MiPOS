using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Compras;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Alta o edición de un proveedor.
/// </summary>
public partial class ProveedorEdicionViewModel : ObservableObject
{
    private readonly CompraServicio _compraServicio;

    // null = proveedor nuevo
    private int? _id;

    public ProveedorEdicionViewModel(CompraServicio compraServicio)
    {
        _compraServicio = compraServicio;
    }

    // true = guardado, false = cancelado. La ventana se cierra al recibirlo.
    public event Action<bool>? Cerrar;

    public bool EsNuevo => _id is null;
    public string Titulo => EsNuevo ? "Nuevo proveedor" : "Editar proveedor";

    public int LargoMaximoNombre => CompraServicio.LargoMaximoNombreProveedor;
    public int LargoMaximoTelefono => CompraServicio.LargoMaximoTelefono;

    // El proveedor guardado (solo si se guardó)
    public Proveedor? ProveedorGuardado { get; private set; }

    public bool Guardando { get; private set; }

    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private string cuit = string.Empty;

    [ObservableProperty]
    private string telefono = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void IniciarNuevo() => _id = null;

    public void IniciarEdicion(Proveedor proveedor)
    {
        _id = proveedor.Id;
        Nombre = proveedor.Nombre;
        // Se muestra con guiones (en la base está solo con números). Nombre completo de la clase
        // porque "Cuit" acá es también el nombre de la propiedad.
        Cuit = proveedor.Cuit is null ? string.Empty : MiPOS.Negocio.Comun.Cuit.Formatear(proveedor.Cuit);
        Telefono = proveedor.Telefono ?? string.Empty;
    }

    // Enter
    [RelayCommand]
    private async Task GuardarAsync()
    {
        try
        {
            Guardando = true;
            Mensaje = "Guardando...";

            if (_id is int id)
            {
                await _compraServicio.ModificarProveedorAsync(id, Nombre, Cuit, Telefono);
                ProveedorGuardado = new Proveedor { Id = id, Nombre = Nombre };
            }
            else
            {
                ProveedorGuardado = await _compraServicio.CrearProveedorAsync(Nombre, Cuit, Telefono);
            }
        }
        catch (Exception ex) when (ex is CompraInvalidaException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
        finally
        {
            Guardando = false;
        }

        if (ProveedorGuardado is not null)
            Cerrar?.Invoke(true);
    }

    // Esc
    [RelayCommand]
    private void Cancelar()
    {
        if (!Guardando)
            Cerrar?.Invoke(false);
    }
}
