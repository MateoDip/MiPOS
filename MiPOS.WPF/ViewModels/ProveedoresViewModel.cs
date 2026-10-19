using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.Compras;
using MiPOS.Negocio.Servicios;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

/// <summary>
/// Lista de proveedores: alta, edición y activar/desactivar.
/// </summary>
public partial class ProveedoresViewModel : ObservableObject
{
    private readonly CompraServicio _compraServicio;
    private readonly DialogoServicio _dialogos;

    public ProveedoresViewModel(CompraServicio compraServicio, DialogoServicio dialogos)
    {
        _compraServicio = compraServicio;
        _dialogos = dialogos;
    }

    // La ventana se cierra al recibirlo
    public event Action? Cerrar;

    public ObservableCollection<Proveedor> Proveedores { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoActivar))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarActivoCommand))]
    private Proveedor? proveedorSeleccionado;

    public string TextoActivar => ProveedorSeleccionado?.Activo == false ? "Activar" : "Desactivar";

    [ObservableProperty]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    // Lo llama DialogoServicio antes de mostrar la ventana
    public void Iniciar() => _ = CargarAsync();

    private async Task CargarAsync(int? seleccionarId = null)
    {
        try
        {
            var proveedores = await _compraServicio.ObtenerProveedoresAsync(incluirInactivos: true);

            Proveedores.Clear();
            foreach (var proveedor in proveedores)
                Proveedores.Add(proveedor);

            ProveedorSeleccionado = Proveedores.FirstOrDefault(p => p.Id == seleccionarId) ?? Proveedores.FirstOrDefault();
            Mensaje = Proveedores.Count == 0 ? "Todavía no hay proveedores. Use \"Nuevo\" para cargar el primero." : string.Empty;
        }
        catch (PermisoDenegadoException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al cargar los proveedores. Verifique la conexión con la base de datos.";
        }
    }

    [RelayCommand]
    private async Task NuevoAsync()
    {
        MensajeExito = string.Empty;
        if (_dialogos.EditarProveedor(null) is { } creado)
        {
            await CargarAsync(creado.Id);
            MensajeExito = "Proveedor creado.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayProveedorSeleccionado))]
    private async Task EditarAsync()
    {
        if (ProveedorSeleccionado is null)
            return;

        MensajeExito = string.Empty;
        var id = ProveedorSeleccionado.Id;
        if (_dialogos.EditarProveedor(ProveedorSeleccionado) is not null)
        {
            await CargarAsync(id);
            MensajeExito = "Cambios guardados.";
        }
    }

    [RelayCommand(CanExecute = nameof(HayProveedorSeleccionado))]
    private async Task CambiarActivoAsync()
    {
        if (ProveedorSeleccionado is null)
            return;

        var proveedor = ProveedorSeleccionado;
        var activar = !proveedor.Activo;
        var pregunta = activar
            ? $"¿Activar a {proveedor.Nombre}?"
            : $"¿Desactivar a {proveedor.Nombre}? No se le podrán cargar compras nuevas.";

        MensajeExito = string.Empty;
        if (!_dialogos.Confirmar(pregunta, activar ? "Activar proveedor" : "Desactivar proveedor"))
            return;

        try
        {
            await _compraServicio.CambiarActivoProveedorAsync(proveedor.Id, activar);
            await CargarAsync(proveedor.Id);
            MensajeExito = activar ? "Proveedor activado." : "Proveedor desactivado.";
        }
        catch (Exception ex) when (ex is CompraInvalidaException or PermisoDenegadoException)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error de conexión con la base de datos. Intente nuevamente.";
        }
    }

    // Esc
    [RelayCommand]
    private void CerrarVentana() => Cerrar?.Invoke();

    private bool HayProveedorSeleccionado() => ProveedorSeleccionado is not null;
}
